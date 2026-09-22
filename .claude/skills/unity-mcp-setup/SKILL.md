---
name: unity-mcp-setup
description: One-time steps to (re)connect the UnityMCP bridge for idle-game-testing when Editor-driven tools (Unity_ManageScene, Unity_ManageGameObject, etc.) aren't available in a session.
---

# Unity MCP setup

It needs a one-time in-Editor setup that can't be scripted from outside Unity:
1. Open this project in Unity (first launch after adding the dependency imports the package).
2. `Window → MCP for Unity → Configure All Detected Clients` — registers the server with Claude Code automatically.
3. Verify with `claude mcp list` from a terminal — `UnityMCP` should show as connected.
