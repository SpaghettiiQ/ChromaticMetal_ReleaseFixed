using UnityEngine;

namespace _Project.Core.Managers
{
    /// <summary>
    /// Shared LineRenderer-based tracer used by hitscan weapons and by abilities that fire a
    /// beam-style projectile (e.g. Target Designator). Each client spawns its own local copy
    /// in response to a server broadcast, so the visual appears for everyone without the
    /// LineRenderer itself being networked.
    /// </summary>
    public static class TracerLine
    {
        private static Material _sharedMaterial;

        public static void SpawnLocal(Vector3 start, Vector3 end, Color color, float width = 0.02f, float lifetime = 0.05f)
        {
            GameObject tracerObject = new GameObject("Tracer");
            tracerObject.transform.position = start;

            LineRenderer line = tracerObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0f);
            line.sharedMaterial = GetMaterial();
            line.SetPosition(0, start);
            line.SetPosition(1, end);

            Object.Destroy(tracerObject, lifetime);
        }

        private static Material GetMaterial()
        {
            if (_sharedMaterial != null) return _sharedMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            _sharedMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return _sharedMaterial;
        }
    }
}
