using UnityEngine;

/// <summary>
/// Simple background looper for exactly N backgrounds (designed for 3).
/// Assign the backgrounds in left-to-right order in the inspector.
/// The script moves them horizontally and when one passes the recycle boundary
/// it is moved behind the others by the total span, creating a queue effect.
/// </summary>
public class BackgroundLooper : MonoBehaviour
{
    [Tooltip("Assign backgrounds in left-to-right order (usually 3).")]
    public Transform[] backgrounds;

    [Tooltip("Movement speed in world units per second. Positive moves backgrounds to the right.")]
    public float moveSpeed = 1f;

    [Tooltip("If true use localPosition for movement/positioning, otherwise use world position.")]
    public bool useLocalSpace = false;

    [Tooltip("If true the script uses unscaled delta time (ignores Time.timeScale).")]
    public bool useUnscaledTime = false;

    [Header("Simple Recycle")]
    [Tooltip("If true, backgrounds will be recycled when their X reaches `recycleX` instead of using bounds-based detection.")]
    public bool useRecycleX = false;
    [Tooltip("World X coordinate threshold used when `useRecycleX` is enabled.")]
    public float recycleX = -919f;
    [Tooltip("If true recycle when position.x <= recycleX, otherwise recycle when >= recycleX.")]
    public bool recycleWhenLessThan = true;
    [Header("Debug / Helpers")]
    [Tooltip("If true the backgrounds array will be sorted by world X at Start (useful for ensuring left->right order).")]
    public bool autoSortByXAtStart = true;
    [Tooltip("Log debug info about positions, edges and recycling events to the Console.")]
    public bool debugLogs = false;
    [Tooltip("If true, use the main camera view edges to decide when a background is off-screen and should be recycled.")]
    public bool useCameraBounds = true;

    // calculated segment length (distance between consecutive backgrounds)
    private float segmentLength = 0f;
    private float totalSpan = 0f; // segmentLength * backgrounds.Length
    private float baseLeftX = 0f; // initial leftmost X (backgrounds[0])
    // per-background widths (world units)
    private float[] widths;

    void Start()
    {
        if (backgrounds == null || backgrounds.Length == 0)
        {
            Debug.LogWarning("BackgroundLooper: No backgrounds assigned, disabling.");
            enabled = false;
            return;
        }

        if (backgrounds.Length < 2)
        {
            Debug.LogWarning("BackgroundLooper: At least 2 backgrounds recommended.");
        }

        // optionally ensure backgrounds are ordered left-to-right in world/local space
        if (autoSortByXAtStart && backgrounds.Length > 1)
        {
            System.Array.Sort(backgrounds, (a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                float ax = GetPosX(a);
                float bx = GetPosX(b);
                return ax.CompareTo(bx);
            });
            if (debugLogs) Debug.Log("BackgroundLooper: auto-sorted backgrounds by X at Start.");
        }

        // compute average distance between consecutive backgrounds (best if arranged contiguously)
        float sum = 0f;
        int count = 0;
        for (int i = 1; i < backgrounds.Length; i++)
        {
            if (backgrounds[i] == null || backgrounds[i - 1] == null) continue;
            float dx = Mathf.Abs(GetPosX(backgrounds[i]) - GetPosX(backgrounds[i - 1]));
            if (dx > 0.0001f)
            {
                sum += dx;
                count++;
            }
        }

        if (count > 0)
            segmentLength = sum / count;

        // fallback: try to get width from SpriteRenderer
        if (segmentLength <= 0f)
        {
            var sr = backgrounds[0].GetComponent<SpriteRenderer>();
            if (sr != null)
                segmentLength = sr.bounds.size.x;
        }

        // final fallback
        if (segmentLength <= 0f)
            segmentLength = 10f;

        // prepare per-background widths
        widths = new float[backgrounds.Length];
        for (int i = 0; i < backgrounds.Length; i++)
        {
            if (backgrounds[i] == null)
            {
                widths[i] = segmentLength;
                continue;
            }
            var sr = backgrounds[i].GetComponent<SpriteRenderer>();
            if (sr != null)
                widths[i] = sr.bounds.size.x;
            else
                widths[i] = segmentLength;
        }

        totalSpan = 0f;
        for (int i = 0; i < widths.Length; i++) totalSpan += widths[i];
        baseLeftX = GetPosX(backgrounds[0]);
    }

    void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float delta = moveSpeed * dt;

        // move all backgrounds
        for (int i = 0; i < backgrounds.Length; i++)
        {
            if (backgrounds[i] == null) continue;
            if (useLocalSpace)
            {
                Vector3 lp = backgrounds[i].localPosition;
                lp.x += delta;
                backgrounds[i].localPosition = lp;
            }
            else
            {
                Vector3 p = backgrounds[i].position;
                p.x += delta;
                backgrounds[i].position = p;
            }
        }
        int n = backgrounds.Length;
        // If using camera bounds, compute world left/right edges of the camera view
        float camLeft = float.NegativeInfinity;
        float camRight = float.PositiveInfinity;
        Camera cam = null;
        if (useCameraBounds)
        {
            cam = Camera.main;
            if (cam != null)
            {
                Vector3 leftWorld = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, Mathf.Abs(cam.transform.position.z - backgrounds[0].position.z)));
                Vector3 rightWorld = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, Mathf.Abs(cam.transform.position.z - backgrounds[0].position.z)));
                camLeft = leftWorld.x;
                camRight = rightWorld.x;
            }
            else if (debugLogs)
            {
                Debug.LogWarning("BackgroundLooper: Camera.main is null, falling back to bounds-free recycling.");
            }
        }

        // Build left/right arrays using SpriteRenderer.bounds where available (world-space)
        float[] lefts = new float[n];
        float[] rights = new float[n];
        for (int i = 0; i < n; i++)
        {
            var t = backgrounds[i];
            if (t == null)
            {
                lefts[i] = float.PositiveInfinity;
                rights[i] = float.NegativeInfinity;
                continue;
            }
            var sr = backgrounds[i].GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                lefts[i] = sr.bounds.min.x;
                rights[i] = sr.bounds.max.x;
                widths[i] = sr.bounds.size.x;
            }
            else
            {
                float posX = GetPosX(t);
                float halfW = (widths != null && i < widths.Length ? widths[i] : segmentLength) * 0.5f;
                lefts[i] = posX - halfW;
                rights[i] = posX + halfW;
            }
            if (debugLogs) Debug.Log($"Background[{i}] posX={GetPosX(t):F2} left={lefts[i]:F2} right={rights[i]:F2}");
        }

        // Decide recycle checks based on camera bounds (preferred) or fallback to inter-background checks
        if (useCameraBounds && cam != null)
        {
            for (int i = 0; i < n; i++)
            {
                if (backgrounds[i] == null) continue;

                float left = lefts[i];
                float right = rights[i];

                if (moveSpeed > 0f)
                {
                    // moving right: when left edge passes camRight, recycle to left of current leftmost
                    if (left > camRight)
                    {
                        float minLeftOther = float.PositiveInfinity;
                        for (int j = 0; j < n; j++) if (j != i && backgrounds[j] != null) minLeftOther = Mathf.Min(minLeftOther, lefts[j]);
                        if (minLeftOther == float.PositiveInfinity) continue;
                        float width = widths != null && i < widths.Length ? widths[i] : segmentLength;
                        float newLeft = minLeftOther - width;
                        float newCenter = newLeft + width * 0.5f;
                        Vector3 p = backgrounds[i].position;
                        p.x = newCenter;
                        backgrounds[i].position = p;
                        lefts[i] = newLeft;
                        rights[i] = newLeft + width;
                        if (debugLogs) Debug.Log($"Background[{i}] recycled (camRight) -> newLeft={newLeft:F2}");
                    }
                }
                else if (moveSpeed < 0f)
                {
                    // moving left: when right edge passes camLeft, recycle to right of current rightmost
                    if (right < camLeft)
                    {
                        float maxRightOther = float.NegativeInfinity;
                        for (int j = 0; j < n; j++) if (j != i && backgrounds[j] != null) maxRightOther = Mathf.Max(maxRightOther, rights[j]);
                        if (maxRightOther == float.NegativeInfinity) continue;
                        float width = widths != null && i < widths.Length ? widths[i] : segmentLength;
                        float newLeft = maxRightOther;
                        float newCenter = newLeft + width * 0.5f;
                        Vector3 p = backgrounds[i].position;
                        p.x = newCenter;
                        backgrounds[i].position = p;
                        lefts[i] = newLeft;
                        rights[i] = newLeft + width;
                        if (debugLogs) Debug.Log($"Background[{i}] recycled (camLeft) -> newLeft={newLeft:F2}");
                    }
                }
            }
        }
        else
        {
            // fallback: use inter-background bounds (previous behavior)
            for (int i = 0; i < n; i++)
            {
                if (backgrounds[i] == null) continue;
                float left = lefts[i];
                float maxRightOther = float.NegativeInfinity;
                float minLeftOther = float.PositiveInfinity;
                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;
                    if (backgrounds[j] == null) continue;
                    if (rights[j] > maxRightOther) maxRightOther = rights[j];
                    if (lefts[j] < minLeftOther) minLeftOther = lefts[j];
                }
                if (maxRightOther == float.NegativeInfinity || minLeftOther == float.PositiveInfinity) continue;
                if (left > maxRightOther)
                {
                    float width = widths != null && i < widths.Length ? widths[i] : segmentLength;
                    float newLeft = minLeftOther - width;
                    float newCenter = newLeft + width * 0.5f;
                    Vector3 p = backgrounds[i].position;
                    p.x = newCenter;
                    backgrounds[i].position = p;
                    lefts[i] = newLeft;
                    rights[i] = newLeft + width;
                    if (debugLogs) Debug.Log($"Background[{i}] recycled (fallback) -> newLeft={newLeft:F2}");
                }
            }
        }
    }

    private float GetPosX(Transform t)
    {
        return useLocalSpace ? t.localPosition.x : t.position.x;
    }

    // Optional debug helper to visualise span in editor
    void OnDrawGizmosSelected()
    {
        if (backgrounds == null || backgrounds.Length == 0) return;
        if (!Application.isPlaying)
        {
            // draw lines between assigned backgrounds
            Gizmos.color = Color.cyan;
            for (int i = 1; i < backgrounds.Length; i++)
            {
                if (backgrounds[i] == null || backgrounds[i - 1] == null) continue;
                Gizmos.DrawLine(backgrounds[i - 1].position, backgrounds[i].position);
            }
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(new Vector3(baseLeftX + totalSpan * 0.5f, 0f, 0f), new Vector3(totalSpan, 0.1f, 0.1f));
        }
    }
}
