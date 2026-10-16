# Keyboard LEDs

Windows tálcaalkalmazás, ami megjeleníti a Num Lock, Caps Lock, Scroll Lock (és opcionálisan az Insert) billentyűk állapotát – tálcaikonon és egy mindig felül lévő, képernyős kijelzőn (overlay).

## Funkciók

- ⌨️ **Num Lock, Caps Lock, Scroll Lock és Insert** állapot kijelzése, kattintással váltható állapot-pirulák
- 🖥️ **Always-on-top overlay** – nem veszi el a fókuszt, átkattintható, képernyőfelvételből kimarad
- 📍 **Pozicionálás** – sarokhoz/élhez rögzítés eltolással, vagy pontos X/Y pixel; elsődleges monitor, az egér monitora vagy az aktív ablak monitora; lecsatolt monitor esetén visszahúzza a képernyőre
- 🔔 **Billentyűnkénti beállítások** – saját név, megjelenjen-e az overlayen, felugorjon-e váltáskor, saját hang (rendszerhang vagy egyéni .wav)
- 🔒 **Állapot kényszerítése** – pl. „Num Lock mindig BE”, „Caps Lock mindig KI”
- 🌗 **Világos / sötét téma** – követi a Windows beállítást (sötét címsorral), a tálcaikon a tálca témájához igazodik
- 🔎 **Éles tálcaikon** minden DPI-n (Per-Monitor V2 DPI aware)
- 🌐 **Magyar / angol felület**, futás közben váltható
- ✨ **Áttűnés animáció**, automatikus elrejtés, „elrejtés ha minden KI”
- 🚀 **Automatikus indulás** a Windows-zal, egyszerre csak egy példány fut

## Futtatás

A kész program a `dist\KeyboardLed.exe`. Két változat készíthető:

| Változat | Méret | Követelmény |
|----------|-------|-------------|
| `dist\KeyboardLed.exe` | ~0,5 MB | [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0) |
| `dist\self-contained\KeyboardLed.exe` | ~70 MB | nincs, bárhol fut |

Ha a program már fut, az exe újbóli elindítása megnyitja a beállítások ablakát (akkor is, ha a tálcaikon el van rejtve).

## Build

Előfeltétel: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
# Tesztek + framework-dependent exe a dist mappába
.\build.ps1

# Önálló (self-contained) exe, .NET telepítés nélkül fut
.\build.ps1 -SelfContained
```

Fejlesztés közben:

```powershell
dotnet run --project KeyboardLed
dotnet test KeyboardLed.Tests
```

## Használat

1. **Beállítások ablak**: dupla klikk a tálcaikonon, jobb klikk → „Beállítások megnyitása”, vagy az exe újbóli elindítása
2. **Billentyű váltása**: kattints a fejlécben az állapot-pirulára, vagy a tálcamenüben a billentyű nevére
3. **Overlay pozícionálás**: válassz rögzítési sarkot és eltolást, vagy a „📍 Húzás” gombbal húzd a helyére
4. **Bezárás**: az ablak bezárása a tálcára rejti a programot; kilépni a „Kilépés” gombbal vagy a tálcamenüből lehet

## Beállítások

A beállítások helye: `%AppData%\KeyboardLed\settings.json` (a régi formátumú fájlt a program automatikusan átalakítja).

| Beállítás | Leírás |
|-----------|--------|
| Indítás a Windows-zal | Automatikus indulás bejelentkezéskor (tálcára rejtve) |
| Tálcaikon megjelenítése | Tálcaikon be/ki |
| Insert billentyű figyelése | Insert állapot kijelzése |
| Nyelv / Téma | Automatikus, magyar/angol; rendszer, világos/sötét |
| Billentyűk | Név, overlay, felugrás, hang, kényszerítés billentyűnként |
| Képernyős kijelző | Be/ki, elrejtés ha minden KI, automatikus elrejtés (1–60 mp), áttűnés |
| Méret / színek / láthatóság | Overlay megjelenés |
| Rögzítés / monitor / eltolás | Overlay pozíció (fizikai pixelben) |

## Hibakeresés

A program naplót ír ide: `%AppData%\KeyboardLed\keyboardled.log`. Váratlan hiba esetén a program tovább fut, és egyszer figyelmeztet a napló helyére.

## Működés

- Az állapotot egy low-level billentyűzet hook eseményei frissítik azonnal; mellette csak egy ritka (500 ms-os) tartalék lekérdezés fut arra az esetre, ha emelt jogú ablak van fókuszban, vagy más program váltja a LED-et.
- A Ctrl+Scroll Lock (Break) nem váltja a Scroll Lock-ot Windows alatt – a program ezt pótolja.

## Licenc

MIT License
