# Keyboard LEDs

A Windows alkalmazás ami megjeleníti a Num Lock, Caps Lock és Scroll Lock billentyűk állapotát.

## Funkciók

- ⌨️ **Num Lock, Caps Lock, Scroll Lock állapot kijelzése**
- 🖥️ **Always-on-top overlay** - minden ablak felett megjelenik
- 📍 **Pixelre pontos pozícionálás** - állítsd be pontosan, hol jelenjen meg az overlay
- 🚀 **Automatikus indulás** - PC bekapcsolásakor automatikusan elindul
- 🔔 **Beep hangjelzés** - állapotváltáskor hangjelzés
- 🎨 **Testreszabható megjelenés** - színek, méret, átlátszóság
- 📊 **System tray ikon** - minimális erőforrás-használat háttérben

## Telepítés

### Előfeltételek

1. **.NET 8.0 SDK** telepítése szükséges:
   - Töltsd le innen: https://dotnet.microsoft.com/download/dotnet/8.0
   - Válaszd a ".NET SDK x64" verziót Windows-ra
   - Telepítés után nyiss új terminált

### Build és futtatás

```powershell
# Navigálj a projekt mappába
cd c:\Users\Peter\Documents\GitHub\KeyBoardLed\KeyboardLed

# Restore (csomagok letöltése)
dotnet restore

# Build
dotnet build

# Futtatás
dotnet run
```

### Release build készítése

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

A kész alkalmazás itt lesz: `bin\Release\net8.0-windows\win-x64\publish\KeyboardLed.exe`

## Használat

1. **Beállítások ablak**: Dupla klikk a tálcaikonon vagy jobb klikk → "Show Settings"
2. **Overlay pozícionálás**: 
   - Írd be pontosan az X és Y koordinátákat, vagy
   - Kattints a "📍 Drag" gombra és húzd az overlay-t a kívánt helyre
3. **Automatikus indulás**: Pipáld be az "Automatically run program on startup" opciót
4. **Minimalizálás**: Az ablak bezárása a tálcára minimalizálja a programot

## Beállítások

| Beállítás | Leírás |
|-----------|--------|
| Auto Start | Program induljon a Windows-szal |
| Beep on Change | Hangjelzés állapotváltáskor |
| Show Overlay | OSD overlay be/ki |
| Hide when all OFF | Elrejtés ha minden ki van kapcsolva |
| Opacity | Átlátszóság (10-100%) |
| Position X/Y | Pixelre pontos pozíció |

## Optimalizálás

- Alacsony memória-használat (~20MB)
- Minimális CPU terhelés (< 1%)
- Hatékony keyboard hook
- Nincs polling - csak eseményvezérelt frissítés

## Licenc

MIT License
