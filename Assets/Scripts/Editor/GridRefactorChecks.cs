using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Focused regression checks; run from menu or CLI without touching the user's scene.</summary>
public static class GridRefactorChecks
{
    public static bool RuntimeChecksPassed { get; private set; }

    public static void BeginRuntimeChecks()
    {
        Require(Application.isPlaying, "Runtime checks require Play Mode");
        RuntimeChecksPassed = false;
        var field = UnityEngine.Object.FindFirstObjectByType<BattleField>();
        field.StartCoroutine(CheckRuntime(field));
    }

    private static IEnumerator CheckRuntime(BattleField field)
    {
        var units = field.Occupants.ToArray();
        var attacker = units.First(unit => unit.name == "Player Vanguard");
        var target = units.First(unit => unit.name == "Enemy Vanguard");
        var scout = units.First(unit => unit.name == "Player Scout");
        var state = field.GetComponent<TacticalStateController>();
        var input = field.GetComponent<BattleFieldInput>();
        var focus = field.GetComponent<CombatFocusDirector>();
        var original = field.Layout;
        GridCoordinate scoutStart = scout.Coordinate;
        bool originalInput = input.enabled;
        try
        {
            input.enabled = false;
            CharacterVisualManager manager = field.GetComponent<CharacterVisualManager>();
            Require(manager != null && manager.RegisteredCount == units.Length, "Visual registration");
            BattleCameraController camera = Camera.main.GetComponent<BattleCameraController>();
            Transform visualRoot = attacker.transform.GetChild(0);
            Quaternion originalCameraRotation = camera.transform.rotation;
            int rotationEvents = 0;
            Action onRotation = () => rotationEvents++;
            camera.RotationChanged += onRotation;
            try
            {
                camera.NotifyRotationChanged();
                int initialEvents = rotationEvents;
                camera.NotifyRotationChanged();
                Require(rotationEvents == initialEvents, "Stationary camera emits no event");
                camera.transform.rotation = Quaternion.Euler(35f, 70f, 0f);
                camera.NotifyRotationChanged();
                Require(rotationEvents == initialEvents + 1, "Camera rotation event");
                Require(Mathf.Abs(Mathf.DeltaAngle(visualRoot.eulerAngles.y, 70f)) < 0.01f,
                    "Registered visual follows camera rotation");
            }
            finally
            {
                camera.RotationChanged -= onRotation;
                camera.transform.rotation = originalCameraRotation;
                camera.NotifyRotationChanged();
            }
            Require(field.TrySetLayout(new GridLayout(original.Width + 1, original.Height + 1,
                original.CellSize + 0.4f, original.CellGap, original.SurfaceHeight)), "Runtime resize");
            CheckActiveScene();
            Require(field.TrySetLayout(original), "Runtime resize restore");
            MethodInfo click = typeof(BattleFieldInput).GetMethod("HandleCellClick", BindingFlags.Instance | BindingFlags.NonPublic);
            click.Invoke(input, new object[] { scout.Coordinate });
            var destination = field.GetReachableCells(scout).First();
            click.Invoke(input, new object[] { destination });
            Require(scout.Coordinate == destination, "Input selection and movement");
            field.ForceMove(scout, scoutStart);
            yield return new WaitForSecondsRealtime(0.3f);
            state.RecalculateAll();
            Require(attacker.State == TacticalState.Standoff && target.State == TacticalState.Standoff,
                "Mutual threat after resize and movement");
            int originalMove = attacker.MoveRange;
            int originalThreat = attacker.ThreatRange;
            input.enabled = true;
            click.Invoke(input, new object[] { attacker.Coordinate });
            attacker.Configure(attacker.Faction, 0, 0);
            Require(attacker.State == TacticalState.Guard, "Range changes recalculate tactical state without movement");
            var shownMoves = (System.Collections.Generic.List<GridCoordinate>)typeof(BattleFieldInput)
                .GetField("moveCells", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(input);
            Require(shownMoves.Count == 0, "Move changes refresh selected range without movement");
            attacker.Configure(attacker.Faction, originalMove, originalThreat);
            Require(attacker.State == TacticalState.Standoff, "Restored threat returns standoff");
            input.enabled = false;
            yield return CheckSpawnLifecycle(field, manager);
            bool resolved = false;
            Action<GridOccupant, GridOccupant, TimingGrade, float> handler = (_, __, ___, ____) => resolved = true;
            focus.AttackResolved += handler;
            try
            {
                Require(focus.TryStartAttack(attacker, target), "Attack accepted");
                float deadline = Time.realtimeSinceStartup + 8f;
                while (focus.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
                Require(!focus.IsBusy && resolved, "Attack completed and result emitted");
                Require(attacker.State == TacticalState.Engaged, "Attack enters engagement");
                Require(Camera.main.GetComponent<BattleCameraController>().enabled, "Camera control restored");
                yield return CheckAttackRecovery(field, attacker, target, focus, camera);
                CheckActiveScene();
                RuntimeChecksPassed = true;
                Debug.Log("RUNTIME GRID CHECKS PASS: registration, rules updates, spawn/remove/pool/death, input/movement, normal attack, cancellation, target removal, subscriber failure recovery.");
            }
            finally { focus.AttackResolved -= handler; }
        }
        finally
        {
            field.TrySetLayout(original);
            if (scout != null && scout.Coordinate != scoutStart) field.ForceMove(scout, scoutStart);
            if (input != null) input.enabled = originalInput;
        }
    }

    private static IEnumerator CheckSpawnLifecycle(BattleField field, CharacterVisualManager manager)
    {
        int registered = manager.RegisteredCount;
        GridCoordinate free = default;
        bool found = false;
        for (int z = 0; z < field.Height && !found; z++)
        for (int x = 0; x < field.Width && !found; x++)
        {
            free = new GridCoordinate(x, z);
            found = !field.GetCell(free).IsOccupied;
        }
        Require(found, "Spawn fixture needs an empty cell");
        GameObject template = new("Unregistered spawn fixture");
        GridOccupant spawned = null;
        try
        {
            template.AddComponent<GridOccupant>().Configure(BattleFaction.Neutral, 1, 0);
            GameObject root = new("Spawn fixture visual");
            root.transform.SetParent(template.transform, false);
            template.AddComponent<GridOccupantVisualizer>().Configure(root.transform, null, null, null);
            Require(field.TrySpawn(template, free, out spawned), "Runtime spawn");
            Require(spawned.transform.parent == field.transform, "Spawn parent belongs to board");
            Require(manager.RegisteredCount == registered + 1, "Spawn registers visual automatically");
            Require(field.TryRemove(spawned), "Explicit removal");
            Require(manager.RegisteredCount == registered, "Removal unregisters visual");
            Require(field.TryPlace(spawned, free), "Re-placement registers visual");
            spawned.gameObject.SetActive(false);
            Require(!field.GetCell(free).IsOccupied && manager.RegisteredCount == registered, "Disable releases cell and visual");
            spawned.gameObject.SetActive(true);
            Require(field.TryPlace(spawned, free) && manager.RegisteredCount == registered + 1, "Pool reuse");
            Character health = spawned.gameObject.AddComponent<Character>();
            health.Stats.hp = 5;
            health.Damaged(5);
            Require(health.IsDead && health.Stats.hp == 0, "Exact-zero HP death");
            Require(!field.GetCell(free).IsOccupied && manager.RegisteredCount == registered, "Death releases cell and visual");
            Require(!field.TryPlace(spawned, free), "Dead unit cannot be placed");
        }
        finally
        {
            if (spawned != null) UnityEngine.Object.Destroy(spawned.gameObject);
            UnityEngine.Object.Destroy(template);
        }
        yield return null;
        Require(manager.RegisteredCount == registered, "Destroyed fixture leaves no registration");
    }

    private static IEnumerator CheckAttackRecovery(BattleField field, GridOccupant attacker, GridOccupant target,
        CombatFocusDirector focus, BattleCameraController camera)
    {
        Vector3 position = camera.transform.position;
        Quaternion rotation = camera.transform.rotation;
        float fov = camera.GetComponent<Camera>().fieldOfView;
        Action checkRestored = () =>
        {
            Require(!focus.IsBusy && camera.enabled, "Cancellation restores camera/input lock");
            Require(Vector3.Distance(camera.transform.position, position) < 0.001f &&
                Quaternion.Angle(camera.transform.rotation, rotation) < 0.01f &&
                Mathf.Abs(camera.GetComponent<Camera>().fieldOfView - fov) < 0.001f, "Camera pose restored");
            var timing = (TimingInputController)new SerializedObject(focus).FindProperty("timingInput").objectReferenceValue;
            Require(timing.GetComponent<CanvasGroup>().alpha == 0f, "Timing HUD hidden on cancel");
        };
        Require(focus.TryStartAttack(attacker, target), "Explicit cancel start");
        focus.CancelAttack();
        checkRestored();
        Require(focus.TryStartAttack(attacker, target), "Disable cancel start");
        focus.enabled = false;
        checkRestored();
        focus.enabled = true;
        GridCoordinate coordinate = target.Coordinate;
        Require(focus.TryStartAttack(attacker, target), "Target removal start");
        field.TryRemove(target);
        checkRestored();
        Require(field.TryPlace(target, coordinate), "Target re-placement");
        camera.enabled = false;
        Require(focus.TryStartAttack(attacker, target), "Disabled camera-controller start");
        focus.CancelAttack();
        Require(!camera.enabled, "Original disabled controller state preserved");
        camera.enabled = true;

        bool reported = false;
        Action<Exception> failed = error => reported = error.Message == "Expected attack subscriber failure";
        Action<GridOccupant, GridOccupant, TimingGrade, float> throwing = (_, __, ___, ____) =>
            throw new InvalidOperationException("Expected attack subscriber failure");
        focus.AttackFailed += failed;
        focus.AttackResolved += throwing;
        try
        {
            Require(focus.TryStartAttack(attacker, target), "Failure injection start");
            float deadline = Time.realtimeSinceStartup + 8f;
            while (focus.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Require(reported && focus.LastFailure != null, "Subscriber failure surfaced to error handler");
            checkRestored();
        }
        finally
        {
            focus.AttackFailed -= failed;
            focus.AttackResolved -= throwing;
            focus.CancelAttack();
        }
        Debug.Log("RECOVERY CHECKS PASS: explicit cancel, disable, removed target, camera-state preservation, injected subscriber exception; no swallowed errors.");
    }
    [MenuItem("Tools/Turn Based Game/Validate Grid Refactor")]
    public static void Run()
    {
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject root = new("Grid regression fixture");
            SceneManager.MoveGameObjectToScene(root, preview);
            BattleField field = root.AddComponent<BattleField>();
            BattleFieldView view = root.AddComponent<BattleFieldView>();
            view.SetMaterial(BattlePrototypeSceneBuilder.GetGridMaterial());
            GridLayout original = new(10, 8, 1.5f, 0.06f, 0.1f);
            Require(field.TrySetLayout(original), "Initial layout");
            GameObject unitObject = new("Test unit");
            unitObject.transform.SetParent(root.transform, false);
            GridOccupant unit = unitObject.AddComponent<GridOccupant>();
            unit.Configure(BattleFaction.Player, 3, 2);
            Require(field.TryPlace(unit, new GridCoordinate(3, 3)), "Placement");

            root.transform.SetPositionAndRotation(new Vector3(5, 2, -4), Quaternion.Euler(0, 37, 0));
            root.transform.localScale = new Vector3(1.7f, 1.2f, 0.8f);
            field.ForceMove(unit, new GridCoordinate(4, 3));
            GridLayout resized = new(12, 9, 2.2f, 0.12f, 0.35f);
            Require(field.TrySetLayout(resized), "Resize accepted");
            Require(view.CellCount == 108, "Visual cell count follows logical count");
            Require(field.GetCell(unit.Coordinate).Occupant == unit, "Resize preserves occupancy");
            Require(Vector3.Distance(unit.transform.position, field.CoordinateToWorld(unit.Coordinate)) < 0.0001f,
                "Resize repositions unit");
            for (int z = 0; z < resized.Height; z++)
            for (int x = 0; x < resized.Width; x++)
            {
                GridCoordinate expected = new(x, z);
                Vector3 center = field.CoordinateToWorld(expected);
                Require(field.TryWorldToCoordinate(center, out var actual) && actual == expected, "Coordinate roundtrip");
                Ray ray = new(center + root.transform.up * 20f, -root.transform.up);
                Require(field.RayToCoordinate(ray, out actual) && actual == expected, "Ray hits correct cell");
            }
            Require(!field.TrySetLayout(new GridLayout(2, 2, 1, 0, 0)), "Unsafe shrink rejected");
            Require(field.Layout.Equals(resized) && view.CellCount == 108, "Rejected resize is atomic");
            Require(!field.TryMove(unit, new GridCoordinate(11, 8)), "Normal movement cannot teleport");
            Require(field.TryMove(unit, new GridCoordinate(5, 3)), "Reachable movement accepted");

            GridOccupant blocker = new GameObject("Blocker").AddComponent<GridOccupant>();
            SceneManager.MoveGameObjectToScene(blocker.gameObject, preview);
            blocker.transform.SetParent(root.transform, false);
            Require(field.TryPlace(blocker, new GridCoordinate(6, 3)), "Blocker placement");
            Require(!field.TryMove(unit, blocker.Coordinate), "Occupied cell rejected");
            Require(!field.GetReachableCells(unit).Contains(blocker.Coordinate), "BFS respects occupancy");

            SerializedObject serialized = new(field);
            serialized.FindProperty("width").intValue = 14;
            serialized.FindProperty("cellSize").floatValue = 1.1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(field.SynchronizeLayout(), "Inspector layout accepted");
            Require(view.CellCount == 14 * 9, "Inspector changes rebuild visual");
            Require(field.GetCell(unit.Coordinate).Occupant == unit, "Inspector preserves occupancy");
            Require(unit.HasSavedCoordinate, "Coordinate persisted for reload");
            Mesh mesh = root.GetComponent<MeshFilter>().sharedMesh;
            Require(mesh.vertexCount == 2 * (field.Width + field.Height + 2), "Line-only grid geometry");
            Require(mesh.GetTopology(0) == MeshTopology.Lines, "Line topology");
            Require(root.GetComponentsInChildren<MeshRenderer>().Count(renderer => renderer.enabled) == 1,
                "Only grid renderer active while idle");
            Require(root.GetComponentsInChildren<Collider>().Length == 0, "No physics dependency");
            Require(!ShaderUtil.ShaderHasError(BattlePrototypeSceneBuilder.GetGridMaterial().shader), "Shader compilation");
            CheckOverlayIsolation(field, view);

            view.enabled = false;
            view.enabled = true;
            Require(view.CellCount == field.Layout.Count, "View enable rebuild");
            field.enabled = false;
            field.enabled = true;
            Require(field.GetCell(unit.Coordinate).Occupant == unit, "Board enable preserves occupancy");
            Debug.Log("GRID CHECKS PASS: coordinate/ray roundtrip (108 cells), resize, shrink rollback, movement, occupancy, Inspector synchronization, resource lifecycle, shader, single renderer.");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static void CheckOverlayIsolation(BattleField field, BattleFieldView view)
    {
        GridCoordinate first = new(0, 0);
        GridCoordinate second = new(1, 0);
        GridCoordinate[] movement = { first, second };
        GridCoordinate[] attacks = { second };
        view.SetRanges(first, movement, attacks);
        int gridBuilds = view.GridBuildCount;
        int rangeBuilds = view.RangeBuildCount;
        int hoverBuilds = view.HoverBuildCount;
        Mesh lines = field.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] originalVertices = lines.vertices;
        view.SetHover(first);
        view.SetHover(second);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) view.SetHover(i % 2 == 0 ? first : second);
        long hoverBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Require(view.GridBuildCount == gridBuilds, "Hover must not rebuild grid lines");
        Require(view.RangeBuildCount == rangeBuilds, "Hover must not rebuild ranges");
        Require(view.HoverBuildCount == hoverBuilds, "Hover must reuse its quad");
        Require(lines.vertices.SequenceEqual(originalVertices), "Fixed mesh vertices unchanged");
        view.SetRanges(first, movement, attacks);
        Require(view.RangeBuildCount == rangeBuilds, "Identical ranges must not upload again");
        view.SetPresentationBrightness(0.4f);
        view.SetPresentationBrightness(1f);
        Require(view.GridBuildCount == gridBuilds && view.RangeBuildCount == rangeBuilds &&
            view.HoverBuildCount == hoverBuilds, "Dimming must not upload geometry");
        view.SetRanges(first, movement, null);
        Require(view.RangeBuildCount == rangeBuilds + 1, "Changed attack flags update range once");
        Require(view.GridBuildCount == gridBuilds, "Range changes leave lines untouched");
        view.SetHover(null);
        Require(!field.transform.Find("Hover Overlay").GetComponent<MeshRenderer>().enabled, "Hover hides outside board");
        view.ClearInteractionHighlights();
        Require(field.GetComponentsInChildren<MeshRenderer>().Count(renderer => renderer.enabled) == 1,
            "Clearing selection disables overlays");
        Debug.Log($"OVERLAY CHECKS PASS: 1000 hover moves, 0 grid/range/hover mesh rebuilds, managed allocation={hoverBytes} B; identical ranges and dimming cause 0 uploads.");
    }

    public static void CheckActiveScene()
    {
        var fields = EditorSceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<BattleField>()).ToArray();
        foreach (BattleField field in fields)
        {
            Require(field.transform.Find("Grid Visuals") == null, "Legacy tile hierarchy removed");
            Require(field.GetComponent<BattleFieldView>().CellCount == field.Layout.Count, "Active scene mesh matches layout");
            foreach (GridOccupant unit in field.Occupants)
            {
                Require(field.GetCell(unit.Coordinate).Occupant == unit, "Active scene occupancy");
                Require(Vector3.Distance(unit.transform.position, field.CoordinateToWorld(unit.Coordinate)) < 0.001f,
                    "Active scene unit alignment");
            }
            Debug.Log($"ACTIVE GRID PASS: {field.Width}x{field.Height}, {field.Occupants.Count()} units, " +
                $"{field.GetComponentsInChildren<MeshRenderer>().Length} mesh renderer(s) including unit markers.");
        }
        Require(fields.Length > 0, "Active board found");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Grid regression failed: " + message);
    }
}
