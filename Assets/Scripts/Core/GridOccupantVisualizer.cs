using System.Collections;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(GridOccupant))]
public sealed class GridOccupantVisualizer : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Renderer blobShadowRenderer;
    [SerializeField] private Renderer stateRingRenderer;
    [SerializeField] private CharacterVisualManager visualManager;
    private MaterialPropertyBlock colorBlock;

    public void BindManager(CharacterVisualManager manager)
    {
        if (visualManager != null) visualManager.Unregister(this);
        visualManager = manager;
        if (manager == null) { selected = false; ResetPresentation(); }
        if (isActiveAndEnabled && visualManager != null) visualManager.Register(this);
    }

    private GridOccupant occupant;
    private Vector3 movementOffset;
    private Vector3 presentationOffset;
    private float presentationAlpha = 1f;
    private bool emphasized;
    private bool selected;
    private Coroutine movementRoutine;

    public void Configure(
        Transform newVisualRoot,
        SpriteRenderer newSpriteRenderer,
        Renderer newBlobShadow,
        Renderer newStateRing)
    {
        visualRoot = newVisualRoot;
        spriteRenderer = newSpriteRenderer;
        blobShadowRenderer = newBlobShadow;
        stateRingRenderer = newStateRing;
        ApplyColors();
    }

    public void SetSelected(bool value)
    {
        selected = value;
        ApplyTransform();
    }

    public void SetPresentation(float alpha, bool focus, Vector3 worldOffset)
    {
        presentationAlpha = Mathf.Clamp01(alpha);
        emphasized = focus;
        presentationOffset = transform.InverseTransformVector(worldOffset);
        ApplyColors();
        ApplyTransform();
    }

    public void ResetPresentation()
    {
        presentationAlpha = 1f;
        emphasized = false;
        presentationOffset = Vector3.zero;
        ApplyColors();
        ApplyTransform();
    }

    public void Flash(Color color, float duration)
    {
        if (Application.isPlaying) StartCoroutine(FlashRoutine(color, duration));
    }

    private void OnEnable()
    {
        occupant = GetComponent<GridOccupant>();
        occupant.StateChanged += OnStateChanged;
        occupant.Moved += OnMoved;
        occupant.RulesChanged += OnRulesChanged;
        if (visualManager != null) visualManager.Register(this);
        ApplyColors();
    }

    private void OnDisable()
    {
        if (visualManager != null) visualManager.Unregister(this);
        if (occupant == null) return;
        occupant.StateChanged -= OnStateChanged;
        occupant.Moved -= OnMoved;
        occupant.RulesChanged -= OnRulesChanged;
        if (movementRoutine != null) StopCoroutine(movementRoutine);
        movementRoutine = null;
        movementOffset = Vector3.zero;
        ResetPresentation();
    }

    private void OnValidate() => ApplyColors();
    private void OnStateChanged(GridOccupant _, TacticalState __, TacticalState ___) => ApplyColors();
    private void OnRulesChanged(GridOccupant _)
    {
        ApplyColors();
        if (visualManager != null) visualManager.RefreshSorting(this);
    }

    private void OnMoved(GridOccupant _, Vector3 previousWorld, Vector3 nextWorld)
    {
        movementOffset += transform.InverseTransformVector(previousWorld - nextWorld);
        if (movementRoutine != null) StopCoroutine(movementRoutine);
        movementRoutine = StartCoroutine(AnimateMovement());
    }

    private IEnumerator AnimateMovement()
    {
        Vector3 start = movementOffset;
        const float duration = 0.22f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            movementOffset = Vector3.LerpUnclamped(start, Vector3.zero, t);
            ApplyTransform();
            yield return null;
        }
        movementOffset = Vector3.zero;
        ApplyTransform();
        movementRoutine = null;
    }

    private IEnumerator FlashRoutine(Color flashColor, float duration)
    {
        if (spriteRenderer == null) yield break;
        Color original = spriteRenderer.color;
        spriteRenderer.color = new Color(flashColor.r, flashColor.g, flashColor.b, presentationAlpha);
        yield return new WaitForSeconds(duration);
        spriteRenderer.color = original;
    }

    public void SetFacing(Vector3 cameraForward)
    {
        if (visualRoot == null) return;
        Vector3 direction = cameraForward;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            visualRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        RefreshSorting(cameraForward);
    }

    public void RefreshSorting(Vector3 cameraForward)
    {
        if (spriteRenderer == null) return;
        int order = Mathf.RoundToInt(-Vector3.Dot(cameraForward, spriteRenderer.transform.position) * 100f);
        if (spriteRenderer.sortingOrder != order) spriteRenderer.sortingOrder = order;
    }

    private void ApplyTransform()
    {
        if (visualRoot == null) return;
        visualRoot.localPosition = movementOffset + presentationOffset;
        float scale = emphasized ? 1.28f : selected ? 1.1f : 1f;
        visualRoot.localScale = Vector3.one * scale;
        visualManager?.RefreshSorting(this);
    }

    private void ApplyColors()
    {
        if (occupant == null) occupant = GetComponent<GridOccupant>();
        if (occupant == null) return;

        Color factionColor = occupant.Faction switch
        {
            BattleFaction.Player => new Color(0.28f, 0.62f, 1f, presentationAlpha),
            BattleFaction.Enemy => new Color(1f, 0.38f, 0.18f, presentationAlpha),
            _ => new Color(0.65f, 0.67f, 0.7f, presentationAlpha)
        };
        if (spriteRenderer != null) spriteRenderer.color = factionColor;

        Color stateColor = occupant.State switch
        {
            TacticalState.Standoff => new Color(1f, 0.72f, 0.08f, presentationAlpha),
            TacticalState.Engaged => new Color(1f, 0.08f, 0.12f, presentationAlpha),
            _ => new Color(0.1f, 0.9f, 0.78f, presentationAlpha)
        };
        SetRendererColor(stateRingRenderer, stateColor);
        SetRendererColor(blobShadowRenderer, new Color(0.02f, 0.025f, 0.03f, 0.55f * presentationAlpha));
    }

    private void SetRendererColor(Renderer renderer, Color color)
    {
        if (renderer == null) return;
        colorBlock ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(colorBlock);
        colorBlock.SetColor("_BaseColor", color);
        colorBlock.SetColor("_Color", color);
        renderer.SetPropertyBlock(colorBlock);
    }
}
