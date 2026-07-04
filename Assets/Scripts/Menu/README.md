# Menú Principal

Sistema del menú principal + base de navegación entre escenas.

## Puesta en marcha (una sola vez)

1. En la barra de menús de Unity: **YGO > Setup > Configurar Menú Principal (escena + Build Settings)**.
   - Crea `Assets/Scenes/MainMenu.unity` con el `MainMenuController`.
   - **Construye la UI como objetos reales** (Canvas, título, botones, paneles) dentro de la
     escena y **cablea las referencias** del controlador.
   - Registra TODAS las escenas de `Assets/Scenes` en *Build Settings*, con `MainMenu` inicial.
2. Pulsa **Play**.

> La UI son **GameObjects normales**: aparecen en la Hierarchy y se editan en el Inspector.
> Mueve, reestiliza y reorganiza lo que quieras — mientras el objeto siga referenciado en el
> `MainMenuController`, todo sigue funcionando. El controlador **no genera nada por código**.

### ¿Rehacer la UI desde cero?

**YGO > Setup > Reconstruir UI del Menú** borra el canvas actual y lo regenera (perderás los
ajustes manuales). Útil solo si quieres volver al punto de partida.

## Archivos

| Archivo | Rol |
|---|---|
| `GameScenes.cs` | Nombres de escena centralizados + comprobación de si están en Build Settings. |
| `GameNavigator.cs` | Singleton persistente. Único punto de carga de escenas (carga segura) y de salir del juego. |
| `SettingsManager.cs` | Singleton persistente. Audio / idioma / pantalla, guardado en PlayerPrefs. |
| `GameProgress.cs` | Save de la aventura (JSON). Decide si "Continuar" está disponible. |
| `ResponsiveCanvasScaler.cs` | Ajusta el Canvas a cualquier relación de aspecto sin recortar. |
| `MainMenuController.cs` | Lógica de los 9 botones y paneles. Usa **referencias del Inspector** (no crea UI). |
| `../../Editor/MainMenuBuilder.cs` | (Editor) Construye la UI como objetos reales y cablea las referencias. |
| `../../Editor/MainMenuSetup.cs` | (Editor) Herramienta del menú **YGO > Setup**. |

## Estado de cada botón

- **Colección** → carga `LibraryScene` (ya existe).
- **Duelo Libre** → por ahora carga `DuelScene` (duelo de prueba). Más adelante debería ir a una pantalla de selección de rival.
- **Opciones** y **Créditos** → paneles funcionales dentro del propio menú.
- **Salir** → cierra el juego (en el editor detiene el Play).
- **Nueva Partida / Continuar / Historia** → esperan la escena `StoryScene` (aún no creada). Mientras no exista, muestran un aviso "Próximamente" en vez de fallar.
- **Constructor de Deck** → espera `DeckBuilderScene` (aún no creada).

## Poner tu propio arte (responsivo)

La escena ya se adapta a **cualquier resolución y relación de aspecto**:

- **`ResponsiveCanvasScaler`** (en el `MainMenuCanvas`) ajusta el escalado por ancho o
  por alto según el aspecto, de modo que nada de lo diseñado dentro de 1920×1080 se recorta.
- Título, subtítulo y toast se estiran a lo ancho y **auto-ajustan** su fuente.
- Los botones viven en un `VerticalLayoutGroup` que se reacomoda solo.

Para tu arte:

| Quiero… | Cómo |
|---|---|
| **Fondo a pantalla completa** | Arrastra tu sprite al **Source Image** del objeto `BackgroundArt`. El componente `ResponsiveBackground` lo hace **cubrir** la pantalla sin deformarse a cualquier aspecto. |
| **Color de fondo base** | Cambia el color del objeto `BackgroundColor` (se ve si el arte no llena algún hueco o mientras no hay sprite). |
| **Fondo de un botón** | En el `Image` del botón, pon tu sprite en *Source Image* y súbelo a tipo *Sliced* (9-slices) para que escale sin deformar bordes. |
| **Logo en vez de texto** | Añade un `Image` con tu logo, ánclalo arriba-centro; escalará con el Canvas. |
| **Un elemento que siga un borde** | Usa los **anchors** del `RectTransform` (esquinas/bordes) en vez de posiciones fijas, y así se pega a ese borde en todas las resoluciones. |

Regla general: para que algo se adapte, **ánclalo** al borde/zona que le corresponde y evita
tamaños en píxeles fijos para cosas que deban crecer con la pantalla.

## Cómo enganchar una pantalla nueva

1. Crea la escena (ej. `Assets/Scenes/DeckBuilderScene.unity`).
2. Vuelve a ejecutar **YGO > Setup > Configurar Menú Principal** (re-registra Build Settings), o añádela a mano en *File > Build Settings*.
3. Si usaste otro nombre, ponlo en el Inspector del `MainMenuController` (campos *Destinos de escena*).

Toda navegación debe pasar por `GameNavigator.Instance.GoTo("NombreEscena")`, nunca por `SceneManager.LoadScene` directo.
