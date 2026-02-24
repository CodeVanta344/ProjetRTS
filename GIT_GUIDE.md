# 🎮 ProjetRTS — Guide Git Équipe

## Règle d'or : NE JAMAIS travailler sur la même scène/préfab en même temps

Unity n'est **pas** fait pour merger des scènes. Les fichiers `.unity`, `.prefab`, `.asset` sont des YAML géants — un merge classique les casse.

---

## Workflow quotidien

### Avant de commencer à travailler
```bash
git pull                          # Récupère les derniers changements
git lfs pull                      # Télécharge les assets LFS mis à jour
```

### Pendant le travail
```bash
git add -A
git commit -m "description claire de ce qui a changé"
```

### Envoyer son travail
```bash
git pull                          # D'abord récupérer les changements du collègue
git push                          # Puis envoyer les siens
```

---

## Répartition des fichiers (IMPORTANT)

Pour éviter les conflits, chacun travaille sur **des fichiers différents** :

| Personne | Zone                                      |
|----------|-------------------------------------------|
| Loïc     | Scripts/Core, Scripts/Units, Scripts/AI   |
| Collègue | (à définir ensemble)                      |

### Fichiers à verrouiller avant d'éditer (scènes, prefabs, modèles)
```bash
# Avant de modifier un fichier binaire/scène
git lfs lock Assets/Scenes/MaScene.unity

# Quand c'est fini
git lfs unlock Assets/Scenes/MaScene.unity
```

Ça empêche l'autre personne de le modifier en même temps.

---

## Branches (optionnel mais recommandé)

Pour les gros changements, utiliser des branches :
```bash
# Créer une branche pour une feature
git checkout -b feature/nom-de-la-feature

# Travailler, committer...

# Quand c'est prêt, merger dans main
git checkout main
git pull
git merge feature/nom-de-la-feature
git push
```

---

## En cas de conflit

### Conflit sur un fichier .cs (code)
Git va marquer les conflits avec `<<<<<<<`. Ouvrir le fichier, choisir la bonne version, puis :
```bash
git add le-fichier-en-conflit.cs
git commit
```

### Conflit sur une scène .unity ou un .prefab
**NE PAS essayer de merger manuellement.** Choisir une version :
```bash
# Garder MA version
git checkout --ours Assets/Scenes/MaScene.unity

# Garder la version du COLLÈGUE
git checkout --theirs Assets/Scenes/MaScene.unity

git add Assets/Scenes/MaScene.unity
git commit
```

---

## Résumé rapide

1. `git pull` avant de bosser
2. Ne pas toucher aux mêmes fichiers en même temps
3. `git lfs lock` pour les scènes/préfabs/modèles
4. Commits fréquents avec messages clairs
5. `git push` souvent pour partager le travail
