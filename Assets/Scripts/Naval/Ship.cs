using UnityEngine;
using NapoleonicWars.Data;

namespace NapoleonicWars.Naval
{
    /// <summary>
    /// Ship controller for naval battles. Handles movement, combat, and damage.
    /// </summary>
    public class Ship : MonoBehaviour
    {
        [Header("Ship Data")]
        [SerializeField] private ShipData shipData;

        // Runtime state
        private float currentHullHP;
        private float currentSailHP;
        private int currentCrew;
        private int currentCannonsLeft;
        private int currentCannonsRight;
        private float currentSpeed;
        private float targetSpeed;
        private bool isOnFire;
        private bool isSinking;
        private bool hasSurrendered;

        // Reload timers
        private float leftBroadsideReload;
        private float rightBroadsideReload;

        // Movement
        private float currentHeading;
        private Vector3 velocity;

        // Selection
        private bool isSelected;
        public int TeamId { get; set; }

        // Public accessors
        public ShipData Data => shipData;
        public float HullHP => currentHullHP;
        public float SailHP => currentSailHP;
        public int Crew => currentCrew;
        public float Speed => currentSpeed;
        public float Heading => currentHeading;
        public bool IsOnFire => isOnFire;
        public bool IsSinking => isSinking;
        public bool HasSurrendered => hasSurrendered;
        public bool IsOperational => !isSinking && !hasSurrendered && currentCrew > 0;
        public bool CanFireLeft => leftBroadsideReload <= 0 && currentCannonsLeft > 0;
        public bool CanFireRight => rightBroadsideReload <= 0 && currentCannonsRight > 0;
        public float HullPercent => currentHullHP / shipData.maxHullHP;
        public float SailPercent => currentSailHP / shipData.maxSailHP;
        public float CrewPercent => (float)currentCrew / shipData.maxCrew;
        public bool IsSelected => isSelected;

        // Events
        public delegate void ShipEvent(Ship ship);
        public event ShipEvent OnSunk;
        public event ShipEvent OnSurrendered;
        public event ShipEvent OnFire;

        public void Initialize(ShipData data, int team)
        {
            shipData = data;
            TeamId = team;

            currentHullHP = data.maxHullHP;
            currentSailHP = data.maxSailHP;
            currentCrew = data.maxCrew;
            currentCannonsLeft = data.cannonsPerBroadside;
            currentCannonsRight = data.cannonsPerBroadside;
            currentSpeed = 0f;
            targetSpeed = 0f;
            currentHeading = transform.eulerAngles.y;

            leftBroadsideReload = 0f;
            rightBroadsideReload = 0f;
        }

        private void Update()
        {
            if (!IsOperational) return;

            UpdateMovement();
            UpdateReload();
            UpdateFire();
            UpdateSinking();
        }

        #region Movement

        private void UpdateMovement()
        {
            // Calculate effective max speed based on sail damage
            float sailEfficiency = currentSailHP / shipData.maxSailHP;
            float effectiveMaxSpeed = shipData.maxSpeed * sailEfficiency;

            // Get wind effect
            float windEffect = 1f;
            if (NavalBattleManager.Instance != null)
            {
                windEffect = NavalBattleManager.Instance.GetWindEffect(currentHeading);
            }
            effectiveMaxSpeed *= windEffect;

            // Accelerate/decelerate towards target speed
            if (currentSpeed < targetSpeed)
            {
                currentSpeed += shipData.acceleration * Time.deltaTime;
                currentSpeed = Mathf.Min(currentSpeed, targetSpeed, effectiveMaxSpeed);
            }
            else if (currentSpeed > targetSpeed)
            {
                currentSpeed -= shipData.acceleration * 0.5f * Time.deltaTime;
                currentSpeed = Mathf.Max(currentSpeed, targetSpeed, 0f);
            }

            // Apply movement
            Vector3 forward = Quaternion.Euler(0, currentHeading, 0) * Vector3.forward;
            velocity = forward * currentSpeed;
            transform.position += velocity * Time.deltaTime;

            // Apply rotation
            transform.rotation = Quaternion.Euler(0, currentHeading, 0);
        }

        public void SetTargetSpeed(float speedPercent)
        {
            float sailEfficiency = currentSailHP / shipData.maxSailHP;
            targetSpeed = shipData.maxSpeed * Mathf.Clamp01(speedPercent) * sailEfficiency;
        }

        public void Turn(float direction)
        {
            // direction: -1 = port (left), +1 = starboard (right)
            float turnSpeed = shipData.turnRate;
            
            // Turning is slower at low speed
            turnSpeed *= Mathf.Clamp(currentSpeed / 3f, 0.3f, 1f);
            
            // Crew affects maneuverability
            turnSpeed *= Mathf.Clamp(CrewPercent, 0.5f, 1f);

            currentHeading += direction * turnSpeed * Time.deltaTime;
            currentHeading = (currentHeading + 360f) % 360f;
        }

        public void SetHeading(float heading)
        {
            currentHeading = (heading + 360f) % 360f;
        }

        #endregion

        #region Combat

        public void FireBroadside(bool leftSide, Ship target, AmmoType ammo = AmmoType.RoundShot)
        {
            if (leftSide && !CanFireLeft) return;
            if (!leftSide && !CanFireRight) return;

            int cannons = leftSide ? currentCannonsLeft : currentCannonsRight;
            float baseDamage = shipData.cannonDamage * cannons;

            // Calculate hit chance based on range and crew
            float distance = Vector3.Distance(transform.position, target.transform.position);
            float hitChance = 1f - (distance / shipData.cannonRange);
            hitChance *= CrewPercent; // Crew affects accuracy
            hitChance = Mathf.Clamp(hitChance, 0.1f, 0.9f);

            // Calculate actual hits
            int hits = 0;
            for (int i = 0; i < cannons; i++)
            {
                if (Random.value < hitChance) hits++;
            }

            float totalDamage = (baseDamage / cannons) * hits;

            // Apply damage based on ammo type
            switch (ammo)
            {
                case AmmoType.RoundShot:
                    target.TakeDamage(ShipSection.Hull, totalDamage);
                    break;
                case AmmoType.ChainShot:
                    target.TakeDamage(ShipSection.Sails, totalDamage * 1.5f);
                    break;
                case AmmoType.Grapeshot:
                    target.TakeDamage(ShipSection.Crew, totalDamage * 0.8f);
                    break;
            }

            // Start reload
            if (leftSide)
                leftBroadsideReload = shipData.reloadTime;
            else
                rightBroadsideReload = shipData.reloadTime;

            // Visual/audio feedback
            CreateBroadsideEffect(leftSide);
        }

        public void TakeDamage(ShipSection section, float damage)
        {
            // Apply armor reduction for hull
            if (section == ShipSection.Hull)
            {
                damage = Mathf.Max(0, damage - shipData.armor);
            }

            switch (section)
            {
                case ShipSection.Hull:
                    currentHullHP -= damage;
                    if (currentHullHP <= 0)
                    {
                        StartSinking();
                    }
                    else if (currentHullHP < shipData.maxHullHP * 0.3f && Random.value < 0.1f)
                    {
                        StartFire();
                    }
                    break;

                case ShipSection.Sails:
                    currentSailHP -= damage;
                    currentSailHP = Mathf.Max(0, currentSailHP);
                    break;

                case ShipSection.Crew:
                    int casualties = Mathf.RoundToInt(damage / 5f);
                    currentCrew -= casualties;
                    currentCrew = Mathf.Max(0, currentCrew);
                    
                    // Check for surrender
                    if (currentCrew < shipData.maxCrew * 0.1f)
                    {
                        CheckSurrender();
                    }
                    break;

                case ShipSection.Cannons:
                    // Randomly disable cannons
                    int disabled = Mathf.RoundToInt(damage / 50f);
                    if (Random.value > 0.5f)
                        currentCannonsLeft = Mathf.Max(0, currentCannonsLeft - disabled);
                    else
                        currentCannonsRight = Mathf.Max(0, currentCannonsRight - disabled);
                    break;
            }
        }

        private void UpdateReload()
        {
            // Reload speed affected by crew
            float reloadMultiplier = Mathf.Clamp(CrewPercent, 0.3f, 1f);

            if (leftBroadsideReload > 0)
                leftBroadsideReload -= Time.deltaTime * reloadMultiplier;
            if (rightBroadsideReload > 0)
                rightBroadsideReload -= Time.deltaTime * reloadMultiplier;
        }

        private void CreateBroadsideEffect(bool leftSide)
        {
            // Create smoke/flash effect
            Vector3 sideDir = leftSide ? -transform.right : transform.right;
            Vector3 effectPos = transform.position + sideDir * 5f;

            // Simple particle burst (would use proper particle system)
            Debug.DrawRay(effectPos, sideDir * 20f, Color.yellow, 0.5f);
        }

        #endregion

        #region Boarding

        public bool CanBoard(Ship target)
        {
            if (!IsOperational || !target.IsOperational) return false;
            
            float distance = Vector3.Distance(transform.position, target.transform.position);
            return distance < 15f && currentCrew >= 20;
        }

        public void InitiateBoarding(Ship target)
        {
            if (!CanBoard(target)) return;

            // Calculate boarding combat
            int ourMarines = Mathf.Min(shipData.marinesCount, currentCrew / 4);
            int theirMarines = Mathf.Min(target.Data.marinesCount, target.Crew / 4);

            float ourStrength = ourMarines * (CrewPercent + 0.5f);
            float theirStrength = theirMarines * (target.CrewPercent + 0.5f);

            // Combat rounds
            for (int round = 0; round < 5; round++)
            {
                int ourLosses = Mathf.RoundToInt(theirStrength * Random.Range(0.1f, 0.3f));
                int theirLosses = Mathf.RoundToInt(ourStrength * Random.Range(0.1f, 0.3f));

                currentCrew -= ourLosses;
                target.currentCrew -= theirLosses;

                ourStrength = ourMarines * (CrewPercent + 0.5f);
                theirStrength = theirMarines * (target.CrewPercent + 0.5f);

                if (target.currentCrew <= 0 || ourStrength > theirStrength * 2)
                {
                    // We captured the ship
                    target.Surrender();
                    break;
                }
                else if (currentCrew <= 0 || theirStrength > ourStrength * 2)
                {
                    // Boarding failed
                    break;
                }
            }
        }

        #endregion

        #region Status Effects

        private void StartFire()
        {
            if (isOnFire) return;
            isOnFire = true;
            OnFire?.Invoke(this);
        }

        private void UpdateFire()
        {
            if (!isOnFire) return;

            // Fire spreads and causes damage
            currentHullHP -= 5f * Time.deltaTime;
            currentCrew -= Mathf.RoundToInt(2f * Time.deltaTime);

            // Crew can fight fire
            if (currentCrew > 50 && Random.value < 0.001f)
            {
                isOnFire = false;
            }

            if (currentHullHP <= 0)
            {
                StartSinking();
            }
        }

        private void StartSinking()
        {
            if (isSinking) return;
            isSinking = true;
            currentSpeed = 0f;
            targetSpeed = 0f;
            OnSunk?.Invoke(this);
        }

        private void UpdateSinking()
        {
            if (!isSinking) return;

            // Ship sinks slowly
            transform.position += Vector3.down * 0.5f * Time.deltaTime;
            transform.Rotate(Vector3.forward, 5f * Time.deltaTime);

            if (transform.position.y < -10f)
            {
                Destroy(gameObject);
            }
        }

        private void CheckSurrender()
        {
            float surrenderChance = 1f - CrewPercent;
            surrenderChance += (1f - HullPercent) * 0.3f;

            if (Random.value < surrenderChance * 0.1f)
            {
                Surrender();
            }
        }

        public void Surrender()
        {
            if (hasSurrendered) return;
            hasSurrendered = true;
            currentSpeed = 0f;
            targetSpeed = 0f;
            OnSurrendered?.Invoke(this);
        }

        #endregion

        #region Selection

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            // Visual feedback
            var renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                // Highlight selected ship
            }
        }

        #endregion

        #region Fireship

        public void DetonateFireship(Ship target)
        {
            if (shipData.shipType != ShipType.Fireship) return;

            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance > 20f) return;

            // Massive damage to target
            float damage = 500f * (1f - distance / 20f);
            target.TakeDamage(ShipSection.Hull, damage);
            target.StartFire();

            // Destroy this ship
            StartSinking();
        }

        #endregion
    }
}
