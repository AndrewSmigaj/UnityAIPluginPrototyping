# Unity AI Assistant - Implementation Checklist

> **Status:** Day 1 MVP in progress
> **Last Updated:** 2025-10-25
> **Architecture:** See [ARCHITECTURE.yaml](ARCHITECTURE.yaml)

---

## Day 1 MVP Tasks

### 1. Project Setup

- [ ] Create directory structure:
  - [ ] `Assets/Editor/AIAssistant/`
  - [ ] `Assets/Editor/AIAssistant/Actions/`
  - [ ] `Assets/Editor/AIAssistant/Models/`
  - [ ] `Assets/Editor/AIAssistant/Settings/`
  - [ ] `Assets/Editor/AIAssistant/Utilities/`

- [ ] Download and install SimpleJSON
  - [ ] Download `SimpleJSON.cs` from https://github.com/Bunny83/SimpleJSON
  - [ ] Place in `Assets/Editor/AIAssistant/Utilities/SimpleJSON.cs`
  - [ ] Verify it compiles

- [ ] Create assembly definition
  - [ ] Create `Assets/Editor/AIAssistant/AIAssistant.asmdef`
  - [ ] Set `includePlatforms: ["Editor"]`
  - [ ] Verify no compilation errors

---

### 2. Data Models & Actions

- [ ] **IAction.cs**
  - [ ] Define interface with `string GetDescription()` method
  - [ ] Namespace: `UnityEditor.AIAssistant`

- [ ] **CreateRectangleAction.cs**
  - [ ] Fields: `string name, float x, float y, float width, float height, string color`
  - [ ] Implement `GetDescription()` → "Create rectangle '{name}' at ({x}, {y}) - {width}x{height} units - color {color}"

- [ ] **CreateCircleAction.cs**
  - [ ] Fields: `string name, float x, float y, float radius, string color`
  - [ ] Implement `GetDescription()` → "Create circle '{name}' at ({x}, {y}) - radius {radius} - color {color}"

- [ ] **ActionPlan.cs**
  - [ ] Fields: `string ResponseId, string Message, List<IAction> Actions, bool Success, string ErrorMessage`

- [ ] **ActionResult.cs**
  - [ ] Fields: `IAction Action, bool Success, string ErrorMessage, GameObject CreatedObject`

---

### 3. Settings

- [ ] **AIAssistantSettings.cs**
  - [ ] Create ScriptableObject class
  - [ ] Fields:
    - [ ] `string Model` (default: "gpt-4o")
    - [ ] `bool PreviewMode` (default: false)
    - [ ] `int TokenBudget` (default: 8000)
    - [ ] `bool AutoIndexOnSave` (default: true)
  - [ ] Property: `string APIKey` (get/set via EditorPrefs)
    - [ ] Get: `EditorPrefs.GetString("AIAssistant_OpenAI_APIKey", "")`
    - [ ] Set: `EditorPrefs.SetString("AIAssistant_OpenAI_APIKey", value)`
  - [ ] Method: `static AIAssistantSettings GetOrCreateSettings()`
  - [ ] Method: `bool ValidateAPIKey()` (non-empty check)

- [ ] Create settings asset instance
  - [ ] Create `Assets/Editor/AIAssistant/Settings/AIAssistantSettings.asset`
  - [ ] Menu item: `Assets/Create/AI Assistant/Settings`

---

### 4. Project Indexer

- [ ] **ProjectIndexer.cs** (static class)
  - [ ] Method: `void IndexAll()`
    - [ ] Call `IndexProject()`, `IndexScenes()`, `IndexScripts()`
    - [ ] Create `Library/AIAssistant/Artifacts/` directories if missing

  - [ ] Method: `void IndexProject()`
    - [ ] Serialize: Unity version, product name, project path, active scene
    - [ ] JSON structure: `{ unityVersion, productName, projectPath, activeScene }`
    - [ ] Output: `Library/AIAssistant/Artifacts/Project/ProjectMetadata.json`
    - [ ] Use `WriteArtifactIfChanged()`

  - [ ] Method: `void IndexScenes()`
    - [ ] Iterate `EditorBuildSettings.scenes`
    - [ ] For each scene:
      - [ ] Open scene (non-additive)
      - [ ] Get root GameObjects
      - [ ] Serialize: name, active, position, childCount
      - [ ] JSON structure: `{ sceneName, scenePath, rootObjects: [...] }`
      - [ ] Output: `Library/AIAssistant/Artifacts/Scenes/{sceneName}.json`
      - [ ] Use `WriteArtifactIfChanged()`

  - [ ] Method: `void IndexScripts()`
    - [ ] Find all `.cs` files under `Assets/`
    - [ ] Extract class names (simple regex: `class\s+(\w+)`)
    - [ ] Extract namespaces (simple regex: `namespace\s+([\w\.]+)`)
    - [ ] JSON structure: `{ scripts: [{ path, className, namespace }, ...] }`
    - [ ] Output: `Library/AIAssistant/Artifacts/Scripts/AllScripts.json`
    - [ ] Use `WriteArtifactIfChanged()`

  - [ ] Method: `string ComputeHash(string json)`
    - [ ] Use SHA256
    - [ ] Return hex string

  - [ ] Method: `bool WriteArtifactIfChanged(string path, string json)`
    - [ ] Compute hash of new JSON
    - [ ] If file exists, compute hash of existing content
    - [ ] If hashes differ or file missing:
      - [ ] Write JSON to file
      - [ ] Return true
    - [ ] Else return false

  - [ ] Method: `void EnsureDirectoryExists(string path)`
    - [ ] Use `Directory.CreateDirectory(Path.GetDirectoryName(path))`

  - [ ] Hook: Subscribe to `EditorSceneManager.sceneSaved`
    - [ ] Check `settings.AutoIndexOnSave`
    - [ ] If true, call `IndexAll()`

---

### 5. Context Builder

- [ ] **ContextBuilder.cs** (static class)
  - [ ] Method: `string BuildContextPack(string userPrompt, int tokenBudget)`
    - [ ] Read artifacts:
      - [ ] `Library/AIAssistant/Artifacts/Project/ProjectMetadata.json`
      - [ ] `Library/AIAssistant/Artifacts/Scenes/{activeSceneName}.json`
      - [ ] `Library/AIAssistant/Artifacts/Scripts/AllScripts.json`
    - [ ] Build text template:
      ```
      You are a Unity scene assistant with the following capabilities:

      Tools Available:
      - createRectangle: Creates a rectangle sprite GameObject at world position
      - createCircle: Creates a circle sprite GameObject at world position

      Instructions:
      - All positions are in Unity world coordinates
      - Colors must be hex format (#RRGGBB)
      - Be helpful and conversational
      - If uncertain, ask clarifying questions

      ---
      Context Pack v0.1

      ## Project Metadata
      {projectMetadata}

      ## Active Scene
      {activeScene}

      ## Scripts
      {allScripts}

      ---
      User Request: {userPrompt}
      ```
    - [ ] Estimate tokens (`EstimateTokens()`)
    - [ ] If exceeds budget, truncate scripts list
    - [ ] Return final string

  - [ ] Method: `int EstimateTokens(string text)`
    - [ ] Return `text.Length / 4`

  - [ ] Method: `string TruncateScriptsList(string scriptsJson, int maxTokens)`
    - [ ] Parse JSON, remove items until under budget
    - [ ] Return truncated JSON

  - [ ] Method: `string ReadArtifact(string path)`
    - [ ] If file exists, return `File.ReadAllText(path)`
    - [ ] Else return `"{}"`

---

### 6. OpenAI Client

- [ ] **OpenAIClient.cs**
  - [ ] Method: `ActionPlan SendRequest(string apiKey, string model, string contextPack, string previousResponseId = null)`
    - [ ] Build request body with `BuildRequestBody()`
    - [ ] Create UnityWebRequest:
      - [ ] `POST https://api.openai.com/v1/responses`
      - [ ] Headers: `Content-Type: application/json`, `Authorization: Bearer {apiKey}`
      - [ ] Body: request JSON
    - [ ] Send request (blocking)
    - [ ] Handle errors:
      - [ ] Network errors → return `ActionPlan { Success = false, ErrorMessage = "..." }`
      - [ ] 429 rate limit → return `ActionPlan { Success = false, ErrorMessage = "Rate limited..." }`
      - [ ] Other HTTP errors → return appropriate error
    - [ ] Parse response with `ParseResponse()`
    - [ ] Return `ActionPlan`

  - [ ] Method: `string BuildRequestBody(string model, string contextPack, string previousResponseId)`
    - [ ] Build JSON manually (no serialization needed):
      ```json
      {
        "model": "{model}",
        "input": "{contextPack}",
        "store": true,
        "previous_response_id": "{previousResponseId}",  // omit if null
        "tools": [...]
      }
      ```
    - [ ] Include tool definitions for `createRectangle` and `createCircle`
    - [ ] Return JSON string

  - [ ] Method: `ActionPlan ParseResponse(string jsonResponse)`
    - [ ] Use SimpleJSON: `var json = JSON.Parse(jsonResponse)`
    - [ ] Extract `responseId = json["id"]`
    - [ ] Iterate `json["items"].AsArray`:
      - [ ] If `type == "message"`: append `content` to `actionPlan.Message`
      - [ ] If `type == "function_call"`:
        - [ ] Extract `functionName = item["function"]["name"]`
        - [ ] Extract `argsJson = item["function"]["arguments"]`
        - [ ] Parse `args = JSON.Parse(argsJson)`
        - [ ] If `functionName == "createRectangle"`:
          - [ ] Create `CreateRectangleAction` with parsed fields
          - [ ] Add to `actionPlan.Actions`
        - [ ] If `functionName == "createCircle"`:
          - [ ] Create `CreateCircleAction` with parsed fields
          - [ ] Add to `actionPlan.Actions`
    - [ ] Return `ActionPlan { ResponseId, Message, Actions, Success = true }`

  - [ ] Security: NEVER log API key or Authorization header

---

### 7. Plan Applier

- [ ] **PlanApplier.cs** (static class)
  - [ ] Static field: `Dictionary<string, Texture2D> _textureCache`

  - [ ] Method: `List<ActionResult> ApplyPlan(List<IAction> actions, bool previewMode)`
    - [ ] `Undo.IncrementCurrentGroup()`
    - [ ] `int undoGroup = Undo.GetCurrentGroup()`
    - [ ] `var results = new List<ActionResult>()`
    - [ ] For each action:
      - [ ] If `action is CreateRectangleAction`: call `CreateRectangleGameObject()`
      - [ ] If `action is CreateCircleAction`: call `CreateCircleGameObject()`
      - [ ] Add result to `results`
    - [ ] `Undo.CollapseUndoOperations(undoGroup)`
    - [ ] Return `results`

  - [ ] Method: `ActionResult CreateRectangleGameObject(CreateRectangleAction action)`
    - [ ] Validate: width > 0, height > 0
    - [ ] Parse color: `var (success, color) = ParseHexColor(action.color)`
    - [ ] If parse failed, return `ActionResult { Success = false, ErrorMessage = "Invalid color" }`
    - [ ] Get unique name: `var uniqueName = GetUniqueGameObjectName(action.name)`
    - [ ] Create GameObject: `var go = new GameObject(uniqueName)`
    - [ ] Add SpriteRenderer: `var sr = go.AddComponent<SpriteRenderer>()`
    - [ ] Get/create texture: `var tex = GetOrCreateRectangleTexture()`
    - [ ] Create sprite: `var sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64)`
    - [ ] Set sprite: `sr.sprite = sprite`
    - [ ] Set color: `sr.color = color`
    - [ ] Set position: `go.transform.position = new Vector3(action.x, action.y, 0)`
    - [ ] Set scale: `go.transform.localScale = new Vector3(action.width, action.height, 1)`
    - [ ] Undo: `Undo.RegisterCreatedObjectUndo(go, "AI Assistant Actions")`
    - [ ] Return `ActionResult { Success = true, CreatedObject = go }`

  - [ ] Method: `ActionResult CreateCircleGameObject(CreateCircleAction action)`
    - [ ] Similar to rectangle, but:
      - [ ] Validate: radius > 0
      - [ ] Use `GetOrCreateCircleTexture()`
      - [ ] Set scale: `go.transform.localScale = new Vector3(action.radius * 2, action.radius * 2, 1)`

  - [ ] Method: `Texture2D GetOrCreateRectangleTexture()`
    - [ ] Check cache: `if (_textureCache.ContainsKey("rectangle")) return _textureCache["rectangle"]`
    - [ ] Create 64x64 white texture
    - [ ] Cache and return

  - [ ] Method: `Texture2D GetOrCreateCircleTexture()`
    - [ ] Check cache: `if (_textureCache.ContainsKey("circle")) return _textureCache["circle"]`
    - [ ] Create 64x64 texture with white circle
    - [ ] Cache and return

  - [ ] Method: `(bool success, Color color) ParseHexColor(string hex)`
    - [ ] Try `ColorUtility.TryParseHtmlString(hex, out Color color)`
    - [ ] Return `(success, color)`

  - [ ] Method: `string GetUniqueGameObjectName(string baseName)`
    - [ ] If no GameObject with `baseName` exists, return `baseName`
    - [ ] Else append number: `baseName (1)`, `baseName (2)`, etc.

---

### 8. AI Assistant Window

- [ ] **AIAssistantWindow.cs**
  - [ ] Inherit from `EditorWindow`
  - [ ] Menu item: `[MenuItem("Window/AI Assistant")]`

  - [ ] State fields:
    - [ ] `string _lastResponseId`
    - [ ] `List<IAction> _pendingActions`
    - [ ] `bool[] _actionCheckboxes`
    - [ ] `string _userPrompt`
    - [ ] `Vector2 _logScrollPosition`
    - [ ] `List<string> _logEntries` (timestamped log messages)

  - [ ] Method: `void OnEnable()`
    - [ ] Load settings
    - [ ] Subscribe to `EditorSceneManager.sceneSaved` event
    - [ ] Initialize log

  - [ ] Method: `void OnDisable()`
    - [ ] Unsubscribe events

  - [ ] Method: `void OnGUI()`
    - [ ] **Header section:**
      - [ ] Title label "AI Assistant"
      - [ ] Button: "Refresh Index" → `ProjectIndexer.IndexAll()`
      - [ ] Button: "Clear Conversation" → `OnClearConversation()`
    - [ ] **Log area:**
      - [ ] `_logScrollPosition = GUILayout.BeginScrollView(_logScrollPosition)`
      - [ ] For each log entry: `GUILayout.Label(entry)`
      - [ ] `GUILayout.EndScrollView()`
    - [ ] **Pending actions (if not null):**
      - [ ] `DisplayPendingActions()`
    - [ ] **Prompt input:**
      - [ ] `_userPrompt = GUILayout.TextArea(_userPrompt, GUILayout.Height(60))`
      - [ ] Button: "Submit" → `OnSubmitPrompt()`

  - [ ] Method: `void DisplayPendingActions()`
    - [ ] Label: "Pending Actions (check to approve):"
    - [ ] For each action:
      - [ ] `_actionCheckboxes[i] = GUILayout.Toggle(_actionCheckboxes[i], action.GetDescription())`
    - [ ] Horizontal layout:
      - [ ] Button: "Execute Selected" → `OnExecuteSelected()`
      - [ ] Button: "Reject All" → `OnRejectAll()`

  - [ ] Method: `void OnSubmitPrompt()`
    - [ ] Validate: non-empty prompt, valid API key
    - [ ] `try {`
      - [ ] `EditorUtility.DisplayProgressBar("AI Assistant", "Checking scene state...", 0.1f)`
      - [ ] If `EditorSceneManager.GetActiveScene().isDirty`: call `ProjectIndexer.IndexAll()`
      - [ ] `EditorUtility.DisplayProgressBar("AI Assistant", "Building context...", 0.3f)`
      - [ ] `var contextPack = ContextBuilder.BuildContextPack(_userPrompt, settings.TokenBudget)`
      - [ ] `EditorUtility.DisplayProgressBar("AI Assistant", "Calling OpenAI API...", 0.6f)`
      - [ ] `var plan = OpenAIClient.SendRequest(settings.APIKey, settings.Model, contextPack, _lastResponseId)`
      - [ ] Parse plan:
        - [ ] If `!plan.Success`: `AppendLog($"[Error] {plan.ErrorMessage}", LogType.Error)`
        - [ ] If `plan.Message != null`: `AppendLog($"[AI] {plan.Message}", LogType.Log)`
        - [ ] If `plan.Actions.Count > 0`:
          - [ ] Set `_pendingActions = plan.Actions`
          - [ ] Initialize `_actionCheckboxes = new bool[plan.Actions.Count]`
          - [ ] `Repaint()`
      - [ ] Save `_lastResponseId = plan.ResponseId`
    - [ ] `} finally {`
      - [ ] `EditorUtility.ClearProgressBar()`
    - [ ] `}`

  - [ ] Method: `void OnExecuteSelected()`
    - [ ] Filter: `var selectedActions = _pendingActions.Where((a, i) => _actionCheckboxes[i]).ToList()`
    - [ ] If none selected: log "No actions selected"
    - [ ] Call `var results = PlanApplier.ApplyPlan(selectedActions, settings.PreviewMode)`
    - [ ] Call `ProjectIndexer.IndexAll()` (re-index to prevent desync)
    - [ ] Log each result:
      - [ ] Success: `AppendLog($"✓ {result.Action.GetDescription()}", LogType.Log)`
      - [ ] Failure: `AppendLog($"✗ {result.Action.GetDescription()} - {result.ErrorMessage}", LogType.Error)`
    - [ ] `AppendLog($"Executed {successCount}/{totalCount} actions", LogType.Log)`
    - [ ] Clear pending state: `_pendingActions = null`, `_actionCheckboxes = null`
    - [ ] `Repaint()`

  - [ ] Method: `void OnRejectAll()`
    - [ ] `AppendLog("[System] Rejected pending actions", LogType.Warning)`
    - [ ] Clear pending state: `_pendingActions = null`, `_actionCheckboxes = null`
    - [ ] `Repaint()`

  - [ ] Method: `void OnClearConversation()`
    - [ ] `_lastResponseId = null`
    - [ ] `AppendLog("[System] Conversation cleared", LogType.Log)`

  - [ ] Method: `void AppendLog(string message, LogType type)`
    - [ ] Prepend timestamp: `$"[{DateTime.Now:HH:mm:ss}] {message}"`
    - [ ] Add to `_logEntries`
    - [ ] `Repaint()`

---

### 9. Testing

- [ ] **Basic functionality:**
  - [ ] Open Window > AI Assistant
  - [ ] Enter API key in settings
  - [ ] Click "Refresh Index" → verify artifacts created in `Library/AIAssistant/Artifacts/`
  - [ ] Type prompt: "create a red rectangle at 0,0 with width 2 and height 1"
  - [ ] Click Submit
  - [ ] Verify: AI message appears in log
  - [ ] Verify: Pending action appears with checkbox
  - [ ] Check action, click "Execute Selected"
  - [ ] Verify: Rectangle appears in scene at (0,0)
  - [ ] Verify: Log shows success message
  - [ ] Verify: Undo works (single undo removes rectangle)

- [ ] **Conversation state:**
  - [ ] Type prompt: "make it bigger"
  - [ ] Verify: AI understands context (previous_response_id sent)
  - [ ] Execute action
  - [ ] Verify: Larger rectangle created

- [ ] **Partial approval:**
  - [ ] Type prompt: "create 3 blue circles"
  - [ ] Verify: 3 pending actions appear
  - [ ] Check only 2 actions
  - [ ] Execute
  - [ ] Verify: Only 2 circles created
  - [ ] Verify: Re-indexing occurs (check artifact timestamp)

- [ ] **Error handling:**
  - [ ] Test with invalid API key → verify error in log
  - [ ] Test with network disconnected → verify error handling
  - [ ] Test with invalid color format → verify partial failure tracking

- [ ] **Progress bar:**
  - [ ] Verify progress bar appears during API call
  - [ ] Test exception scenario (disconnect during call) → verify progress bar clears

---

## Known Limitations (Day 1)

- No async/await (UI blocks during API call)
- Only creates rectangles and circles
- No component indexing (only GameObject names/positions)
- No conversation persistence across sessions
- Approximate token counting
- Minimal API key validation
- No retry logic for rate limits
- No cost tracking

---

## Future Enhancements (Phase 2+)

### UX Improvements
- Add async/await for non-blocking API calls
- Improve progress bar (percentage, cancelable)
- Add retry logic for rate limits
- Better log area (syntax highlighting, timestamps, expand/collapse)
- Settings UI panel in EditorWindow
- Conversation history panel
- Fork conversation feature
- Cost/token usage tracking
- Persist conversation across sessions

### Advanced Features
- More shape types (triangles, polygons, lines)
- Modify actions (changeColor, move, rotate, scale)
- Delete actions
- Component indexing (Transform, Rigidbody, etc.)
- Query actions (findGameObject, listComponents)
- Prefab instantiation
- Material/shader modifications
- Spatial reasoning
- Hierarchical operations

### Production Readiness
- Unity Test Framework editor tests
- Mock API tests
- Comprehensive documentation
- Sample project
- UPM package
- CI/CD
- Telemetry

---

## Notes

- See `ARCHITECTURE.yaml` for detailed specifications
- SimpleJSON license: MIT (https://github.com/Bunny83/SimpleJSON)
- Always use try/finally for progress bars
- Re-index after execution to prevent conversation desync
- Check scene.isDirty before API call for fresh state
- Keep OpenAI tool definitions in sync with action classes
- Monitor API costs (GPT-4 is expensive)
