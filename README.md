# QR Code Scanner

Un QR code affiché à l'écran, dans une visio, sur une photo, dans un PDF ? Appuie sur **F9** :
l'app capture l'écran, décode le code et te colle le contenu dans le presse-papiers.

Pas de webcam, pas de téléphone, pas de site web à qui envoyer ta capture.

- **Un seul fichier `.exe`**, environ 600 Ko, rien à installer
- Marche sur **Windows 7 → 11** (le .NET Framework 4.8 est déjà dans Windows 10/11)
- Fonctionne sur **tous les écrans** en même temps, et détecte **plusieurs QR codes** d'un coup
- Vit dans la zone de notification, ne consomme rien tant que tu n'appuies pas sur la touche

## Installation

1. Télécharge `QRCodeScanner.exe` depuis la page [Releases](../../releases/latest).
2. Pose-le où tu veux (Bureau, `Documents`, une clé USB…).
3. Double-clique. Une icône violette apparaît en bas à droite, près de l'horloge.

> **« Windows a protégé votre ordinateur »**
> C'est normal : l'exe n'est pas signé par un certificat payant, donc SmartScreen se méfie
> de tout ce qu'il n'a jamais vu. Clique sur **Informations complémentaires**, puis sur
> **Exécuter quand même**. Le code source est entièrement dans ce dépôt si tu veux vérifier,
> et tu peux le recompiler toi-même (voir plus bas).

## Utilisation

| Action | Effet |
| --- | --- |
| **F9** | Scanne tous les écrans et affiche le contenu du QR code |
| Double-clic sur l'icône | Pareil que F9 |
| Clic droit sur l'icône | Menu : historique, réglages, démarrage automatique, quitter |

Le résultat est copié dans le presse-papiers automatiquement, et une petite fenêtre affiche le
texte complet. Si c'est un lien, un bouton **Ouvrir le lien** apparaît — rien ne s'ouvre tout
seul, parce qu'un QR code peut contenir n'importe quelle adresse.

Astuce : ça marche aussi sur un QR code imprimé, en le montrant à ta webcam via l'application
*Caméra* de Windows, ou sur un QR code reçu par téléphone en affichant la photo à l'écran.

## Réglages

Clic droit sur l'icône → **Ouvrir les réglages…**, puis **Recharger les réglages** après avoir
enregistré. Le fichier vit dans `%APPDATA%\QRCodeScanner\QRCodeScanner.ini` :

```ini
Hotkey=F9                  ; Ctrl+Shift+Q, Alt+S, Win+F9… fonctionnent aussi
CopyToClipboard=1          ; copier le résultat automatiquement
ShowResultWindow=1         ; afficher la fenêtre de résultat
OpenUrlAutomatically=0     ; ouvrir le lien sans rien demander (déconseillé)
PlaySound=1                ; petit son de confirmation
ShowNotification=1         ; bulle dans la zone de notification
```

**Mode portable** : si tu places un `QRCodeScanner.ini` juste à côté de l'exe, c'est celui-là
qui est lu. Pratique sur une clé USB, rien n'est écrit dans le profil utilisateur.

Si F9 est déjà pris par une autre application (certains jeux, OBS, Photoshop…), l'app te le dit
au démarrage : change simplement `Hotkey=` pour autre chose.

## Compiler soi-même

Il faut le [SDK .NET](https://dotnet.microsoft.com/download) (8 ou plus récent) :

```powershell
git clone https://github.com/Mushurelie/QRCodeScanner.git
cd QRCodeScanner
.\build.ps1
```

L'exe atterrit dans `dist\QRCodeScanner.exe`.

Le projet cible `net48`, mais le SDK récupère tout seul les assemblies de référence
nécessaires : pas besoin d'installer un vieux dev pack.

## Comment ça marche

| Morceau | Rôle |
| --- | --- |
| `HotKey.cs` | `RegisterHotKey` sur une fenêtre invisible : la touche est réservée pour tout le système, même quand une autre app a le focus |
| `ScreenCapture.cs` | `BitBlt` avec `CAPTUREBLT` sur l'écran virtuel entier, donc tous les moniteurs d'un coup, y compris les fenêtres en surimpression |
| `QrDecoder.cs` | [ZXing.Net](https://github.com/micjahn/ZXing.Net) en mode `TryHarder`, avec des passes à 1x, 0,5x et 2x pour rattraper les codes très grands ou minuscules |
| `TrayContext.cs` | L'icône, le menu, l'historique, et le va-et-vient entre le thread UI et le scan |

L'application est déclarée **PerMonitorV2** dans son manifeste. Sans ça, Windows renverrait une
capture mise à l'échelle et floue sur un écran en 150 %, illisible pour un décodeur.

Contrepartie : WinForms .NET Framework n'active sa propre gestion du DPI qu'avec un fichier
`.exe.config` à côté du binaire, ce qui casserait le principe du fichier unique. `Control.DeviceDpi`
renvoie donc toujours 96 et `AutoScaleMode` est inutilisable. Les dimensions des fenêtres sont donc
multipliées à la main (`Dpi.cs`) — les polices, elles, sont déjà rendues au bon DPI par GDI+.

La DLL ZXing est embarquée dans l'exe comme ressource et rechargée depuis la mémoire au premier
usage (`Program.ResolveEmbedded`), ce qui permet de distribuer un fichier unique.

### Ligne de commande

Pratique pour tester ou scripter :

```powershell
QRCodeScanner.exe --scan-now                    # capture l'écran, écrit les résultats sur stdout
QRCodeScanner.exe --scan-file capture.png       # décode un fichier image
QRCodeScanner.exe --make-qr "coucou" test.png   # génère un QR code
QRCodeScanner.exe --diag                        # DPI détecté, taille de l'écran virtuel
```

Code de sortie `0` si un code a été trouvé, `1` sinon.

`--diag` sert à comprendre un souci d'affichage à distance : sur un écran à 125 %, il doit
afficher `scale = 1,25` et la vraie résolution en pixels (par exemple `1920x1080`, pas
`1536x864`).

## Vie privée

Tout se passe en local. L'application n'ouvre aucune connexion réseau, n'envoie rien nulle part,
et ne garde l'historique qu'en mémoire — il disparaît quand tu quittes.

## Licence

MIT — voir [LICENSE](LICENSE).
