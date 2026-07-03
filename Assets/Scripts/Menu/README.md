# Menú Principal

Sistema del menú principal + base de navegación entre escenas.

## Puesta en marcha (una sola vez)

1. En la barra de menús de Unity: **YGO > Setup > Configurar Menú Principal (escena + Build Settings)**.
   - Crea `Assets/Scenes/MainMenu.unity` con un GameObject que ya tiene `MainMenuController`.
   - Registra TODAS las escenas de `Assets/Scenes` en *Build Settings*, con `MainMenu` como escena inicial (índice 0).
2. Pulsa **Play**. El menú se dibuja solo (Canvas, título, botones, fondo) — no hay que montar UI a mano.

## Archivos

| Archivo | Rol |
|---|---|
| `GameScenes.cs` | Nombres de escena centralizados + comprobación de si están en Build Settings. |
| `GameNavigator.cs` | Singleton persistente. Único punto de carga de escenas (carga segura) y de salir del juego. |
| `SettingsManager.cs` | Singleton persistente. Audio / idioma / pantalla, guardado en PlayerPrefs. |
| `GameProgress.cs` | Save de la aventura (JSON). Decide si "Continuar" está disponible. |
| `MainMenuController.cs` | Lógica de los 9 botones y los paneles de Opciones/Créditos. |
| `MainMenuUIBuilder.cs` | Construye la interfaz por código (sustituible por un prefab diseñado en el futuro). |
| `../../Editor/MainMenuSetup.cs` | Herramienta de editor para el "Puesta en marcha". |

## Estado de cada botón

- **Colección** → carga `LibraryScene` (ya existe).
- **Duelo Libre** → por ahora carga `DuelScene` (duelo de prueba). Más adelante debería ir a una pantalla de selección de rival.
- **Opciones** y **Créditos** → paneles funcionales dentro del propio menú.
- **Salir** → cierra el juego (en el editor detiene el Play).
- **Nueva Partida / Continuar / Historia** → esperan la escena `StoryScene` (aún no creada). Mientras no exista, muestran un aviso "Próximamente" en vez de fallar.
- **Constructor de Deck** → espera `DeckBuilderScene` (aún no creada).

## Cómo enganchar una pantalla nueva

1. Crea la escena (ej. `Assets/Scenes/DeckBuilderScene.unity`).
2. Vuelve a ejecutar **YGO > Setup > Configurar Menú Principal** (re-registra Build Settings), o añádela a mano en *File > Build Settings*.
3. Si usaste otro nombre, ponlo en el Inspector del `MainMenuController` (campos *Destinos de escena*).

Toda navegación debe pasar por `GameNavigator.Instance.GoTo("NombreEscena")`, nunca por `SceneManager.LoadScene` directo.
