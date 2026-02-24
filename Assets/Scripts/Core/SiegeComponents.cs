using UnityEngine;
using NapoleonicWars.Units;

namespace NapoleonicWars.Core
{
    /// <summary>
    /// Wall section that can be damaged and breached during siege battles.
    /// </summary>
    public class WallSection : MonoBehaviour
    {
        private float maxHP;
        private float currentHP;
        private float wallHeight;
        private bool isBreached;
        private Renderer wallRenderer;
        private Color originalColor;

        public float WallHeight => wallHeight;
        public float HPPercent => currentHP / maxHP;
        public bool IsBreached => isBreached;

        public delegate void WallEvent(WallSection wall);
        public event WallEvent OnBreached;
        public event WallEvent OnDamaged;

        public void Initialize(float hp, float height)
        {
            maxHP = hp;
            currentHP = hp;
            wallHeight = height;
            isBreached = false;

            wallRenderer = GetComponent<Renderer>();
            if (wallRenderer != null)
            {
                originalColor = wallRenderer.material.color;
            }
        }

        public void TakeDamage(float damage)
        {
            if (isBreached) return;

            currentHP -= damage;
            OnDamaged?.Invoke(this);

            // Visual feedback - darken as damaged
            if (wallRenderer != null)
            {
                float healthPercent = currentHP / maxHP;
                wallRenderer.material.color = Color.Lerp(Color.black, originalColor, healthPercent);
            }

            if (currentHP <= 0)
            {
                CreateBreach();
            }
        }

        private void CreateBreach()
        {
            isBreached = true;

            // Visual - create hole in wall
            if (wallRenderer != null)
            {
                // Simple approach: make wall semi-transparent and darker
                Color breachColor = new Color(0.2f, 0.15f, 0.1f, 0.5f);
                wallRenderer.material.color = breachColor;
            }

            // Disable collider to allow passage
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            // Create rubble pile (visual)
            CreateRubble();

            OnBreached?.Invoke(this);
        }

        private void CreateRubble()
        {
            // Create some rubble cubes
            for (int i = 0; i < 5; i++)
            {
                GameObject rubble = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rubble.transform.position = transform.position + new Vector3(
                    Random.Range(-3f, 3f),
                    Random.Range(0.5f, 2f),
                    Random.Range(-2f, 2f)
                );
                rubble.transform.localScale = Vector3.one * Random.Range(0.5f, 1.5f);
                rubble.transform.rotation = Random.rotation;

                var renderer = rubble.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(0.4f, 0.35f, 0.3f);
                }

                // Add physics
                var rb = rubble.AddComponent<Rigidbody>();
                rb.mass = 100f;
            }
        }

        public void Repair(float amount)
        {
            if (!isBreached)
            {
                currentHP = Mathf.Min(maxHP, currentHP + amount);
                
                if (wallRenderer != null)
                {
                    float healthPercent = currentHP / maxHP;
                    wallRenderer.material.color = Color.Lerp(Color.black, originalColor, healthPercent);
                }
            }
        }
    }

    /// <summary>
    /// Gate that can be destroyed by rams or artillery.
    /// </summary>
    public class GateController : MonoBehaviour
    {
        private float maxHP;
        private float currentHP;
        private bool isDestroyed;
        private bool isOpen;
        private Renderer gateRenderer;
        private Color originalColor;

        public float HPPercent => currentHP / maxHP;
        public bool IsDestroyed => isDestroyed;
        public bool IsOpen => isOpen;

        public delegate void GateEvent();
        public event GateEvent OnGateDestroyed;
        public event GateEvent OnGateOpened;

        public void Initialize(float hp)
        {
            maxHP = hp;
            currentHP = hp;
            isDestroyed = false;
            isOpen = false;

            gateRenderer = GetComponent<Renderer>();
            if (gateRenderer != null)
            {
                originalColor = gateRenderer.material.color;
            }
        }

        public void TakeDamage(float damage)
        {
            if (isDestroyed) return;

            currentHP -= damage;

            // Visual feedback
            if (gateRenderer != null)
            {
                float healthPercent = currentHP / maxHP;
                gateRenderer.material.color = Color.Lerp(new Color(0.1f, 0.05f, 0f), originalColor, healthPercent);
            }

            if (currentHP <= 0)
            {
                DestroyGate();
            }
        }

        private void DestroyGate()
        {
            isDestroyed = true;

            // Hide gate
            if (gateRenderer != null)
            {
                gateRenderer.enabled = false;
            }

            // Disable collider
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            // Create debris
            CreateDebris();

            OnGateDestroyed?.Invoke();
        }

        private void CreateDebris()
        {
            for (int i = 0; i < 8; i++)
            {
                GameObject debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
                debris.transform.position = transform.position + new Vector3(
                    Random.Range(-2f, 2f),
                    Random.Range(0.2f, 1f),
                    Random.Range(-1f, 1f)
                );
                debris.transform.localScale = new Vector3(
                    Random.Range(0.3f, 1f),
                    Random.Range(0.1f, 0.5f),
                    Random.Range(0.3f, 0.8f)
                );
                debris.transform.rotation = Random.rotation;

                var renderer = debris.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(0.25f, 0.15f, 0.05f); // Wood color
                }

                var rb = debris.AddComponent<Rigidbody>();
                rb.mass = 20f;
            }
        }

        public void OpenGate()
        {
            if (isDestroyed) return;

            isOpen = true;
            
            // Animate gate opening (simple version - just disable collider)
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            OnGateOpened?.Invoke();
        }

        public void CloseGate()
        {
            if (isDestroyed) return;

            isOpen = false;

            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = true;
            }
        }
    }

    /// <summary>
    /// Defensive tower that automatically fires at enemies.
    /// </summary>
    public class TowerDefense : MonoBehaviour
    {
        private float damage;
        private float range;
        private float fireRate = 2f; // Shots per second
        private float fireTimer;
        private int teamId = 1; // Defender team

        private UnitBase currentTarget;

        public void Initialize(float damage, float range)
        {
            this.damage = damage;
            this.range = range;
        }

        private void Update()
        {
            fireTimer -= Time.deltaTime;

            // Find target
            if (currentTarget == null || !IsValidTarget(currentTarget))
            {
                FindTarget();
            }

            // Fire at target
            if (currentTarget != null && fireTimer <= 0f)
            {
                FireAtTarget();
                fireTimer = 1f / fireRate;
            }
        }

        private void FindTarget()
        {
            currentTarget = null;
            float closestDist = range;

            // Find closest enemy
            if (SiegeBattleManager.Instance != null)
            {
                foreach (var regiment in SiegeBattleManager.Instance.AttackerRegiments)
                {
                    foreach (var unit in regiment.Units)
                    {
                        if (!IsValidTarget(unit)) continue;

                        float dist = Vector3.Distance(transform.position, unit.transform.position);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            currentTarget = unit;
                        }
                    }
                }
            }
        }

        private bool IsValidTarget(UnitBase unit)
        {
            if (unit == null) return false;
            if (unit.CurrentState == UnitState.Dead) return false;
            if (unit.TeamId == teamId) return false;

            float dist = Vector3.Distance(transform.position, unit.transform.position);
            return dist <= range;
        }

        private void FireAtTarget()
        {
            if (currentTarget == null) return;

            // Apply damage
            currentTarget.TakeDamage(damage, null);

            // Visual effect - simple line
            Debug.DrawLine(transform.position + Vector3.up * 10f, 
                currentTarget.transform.position, Color.red, 0.5f);

            // Could add particle effect here
        }
    }

    /// <summary>
    /// Capture point that attackers must control to win.
    /// </summary>
    public class CapturePoint : MonoBehaviour
    {
        private string pointName;
        private float captureTime;
        private float captureProgress;
        private float pointValue;
        private bool isCaptured;
        private int capturingTeam = -1;
        private int controllingTeam = -1;

        private Renderer indicator;

        public string PointName => pointName;
        public float CaptureProgress => captureProgress;
        public bool IsCaptured => isCaptured;
        public int CapturingTeam => capturingTeam;
        public int ControllingTeam => controllingTeam;
        public float PointValue => pointValue;

        public delegate void CaptureEvent(CapturePoint point, int team);
        public event CaptureEvent OnCaptured;
        public event CaptureEvent OnContested;

        public void Initialize(string name, float captureTime, float value = 1f)
        {
            this.pointName = name;
            this.captureTime = captureTime;
            this.pointValue = value;
            this.captureProgress = 0f;
            this.isCaptured = false;

            indicator = GetComponentInChildren<Renderer>();
        }

        public void UpdateCapture(int attackersInZone, int defendersInZone, float deltaTime)
        {
            if (isCaptured) return;

            // Determine who is capturing
            if (attackersInZone > 0 && defendersInZone == 0)
            {
                // Attackers capturing
                capturingTeam = 0;
                captureProgress += deltaTime / captureTime * attackersInZone * 0.5f;
                captureProgress = Mathf.Min(1f, captureProgress);

                UpdateIndicatorColor();

                if (captureProgress >= 1f)
                {
                    CompleteCaptureBy(0);
                }
            }
            else if (defendersInZone > 0 && attackersInZone == 0)
            {
                // Defenders recapturing / holding
                if (captureProgress > 0f)
                {
                    captureProgress -= deltaTime / captureTime * defendersInZone * 0.3f;
                    captureProgress = Mathf.Max(0f, captureProgress);
                    UpdateIndicatorColor();
                }
            }
            else if (attackersInZone > 0 && defendersInZone > 0)
            {
                // Contested - no progress
                capturingTeam = -1;
                OnContested?.Invoke(this, -1);
            }
            else
            {
                // Empty - slowly decay progress
                if (captureProgress > 0f)
                {
                    captureProgress -= deltaTime / captureTime * 0.1f;
                    captureProgress = Mathf.Max(0f, captureProgress);
                    UpdateIndicatorColor();
                }
            }
        }

        private void UpdateIndicatorColor()
        {
            if (indicator == null) return;

            // Color based on capture progress
            // Yellow (neutral) -> Blue (attacker capturing) -> Red (defender)
            if (captureProgress <= 0f)
            {
                indicator.material.color = new Color(1f, 1f, 0f, 0.5f); // Yellow
            }
            else if (capturingTeam == 0)
            {
                // Attacker capturing - yellow to blue
                indicator.material.color = Color.Lerp(
                    new Color(1f, 1f, 0f, 0.5f),
                    new Color(0f, 0.5f, 1f, 0.7f),
                    captureProgress
                );
            }
        }

        private void CompleteCaptureBy(int team)
        {
            isCaptured = true;
            controllingTeam = team;

            if (indicator != null)
            {
                indicator.material.color = team == 0 
                    ? new Color(0f, 0.5f, 1f, 0.8f)  // Blue for attacker
                    : new Color(1f, 0.2f, 0.2f, 0.8f); // Red for defender
            }

            OnCaptured?.Invoke(this, team);
        }

        public void Reset()
        {
            isCaptured = false;
            captureProgress = 0f;
            capturingTeam = -1;
            controllingTeam = -1;

            if (indicator != null)
            {
                indicator.material.color = new Color(1f, 1f, 0f, 0.5f);
            }
        }
    }
}
