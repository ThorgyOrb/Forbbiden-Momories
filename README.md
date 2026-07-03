# Memorias Prohibidas

Sucesor espiritual de **Yu-Gi-Oh! Forbidden Memories** — un RPG de duelos de cartas para un jugador, hecho en Unity.

> Proyecto en desarrollo temprano.

## Requisitos

- **Unity 2022.3.37f1** (LTS)
- **Git LFS** (`git lfs install`) — el repo guarda modelos 3D, texturas y audio con LFS.

## Cómo abrir

1. Clona el repositorio:
   ```bash
   git clone <url-del-repo>
   git lfs pull
   ```
2. Ábrelo con Unity Hub usando la versión **2022.3.37f1**.
3. La primera vez, ejecuta el menú **YGO > Setup > Configurar Menú Principal** para crear la escena inicial y registrar las escenas en *Build Settings*.
4. Abre `Assets/Scenes/MainMenu.unity` y pulsa **Play**.

## Estructura

```
Assets/
  Scripts/
    Card/       Cartas (CardData, display, efectos)
    Duel/       Motor de duelo (DuelManager, IA, combate, fusiones)
    Library/    Colección y catálogo de cartas
    Menu/       Menú principal y navegación entre escenas
    Oponent/    Datos de oponentes
  Resources/    Cartas, oponentes, prefabs (cargados en runtime)
  Scenes/       Escenas del juego
```

## Sistemas implementados

- Cartas como `ScriptableObject` (monstruo / magia / equipo), con rareza, Guardian Stars y fusiones.
- Motor de duelo por fases (invocación, set, magia, fusión, combate, recompensas).
- Colección del jugador persistente (progreso en JSON).
- Menú principal con navegación, opciones y créditos.

## Créditos

Diseño y programación: **ThorgyOrb**. Motor: Unity.
