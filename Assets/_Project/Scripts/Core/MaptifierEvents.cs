using UnityEngine;

namespace Maptifier.Core
{
    public enum ToolType
    {
        Select,
        Warp,
        Mask,
        Draw,
        Text,
        Effects
    }

    public enum PerformanceTier
    {
        Quality,
        Balanced,
        Performance
    }

    public enum BlendMode
    {
        Normal,
        Additive,
        Multiply,
        Screen,
        Overlay,
        Difference
    }

    public readonly struct DisplayConnectedEvent
    {
        public readonly int DisplayId;
        public readonly Vector2Int Resolution;
        public readonly float RefreshRate;

        public DisplayConnectedEvent(int displayId, Vector2Int resolution, float refreshRate)
        {
            DisplayId = displayId;
            Resolution = resolution;
            RefreshRate = refreshRate;
        }
    }

    public readonly struct DisplayDisconnectedEvent
    {
        public readonly int DisplayId;

        public DisplayDisconnectedEvent(int displayId)
        {
            DisplayId = displayId;
        }
    }

    public readonly struct LayerSelectedEvent
    {
        public readonly int LayerIndex;

        public LayerSelectedEvent(int layerIndex)
        {
            LayerIndex = layerIndex;
        }
    }

    public readonly struct ToolChangedEvent
    {
        public readonly ToolType Tool;

        public ToolChangedEvent(ToolType tool)
        {
            Tool = tool;
        }
    }

    public readonly struct ProjectSavedEvent
    {
        public readonly string ProjectId;
        public readonly string ProjectName;

        public ProjectSavedEvent(string projectId, string projectName)
        {
            ProjectId = projectId;
            ProjectName = projectName;
        }
    }

    public readonly struct ProjectLoadedEvent
    {
        public readonly string ProjectId;

        public ProjectLoadedEvent(string projectId)
        {
            ProjectId = projectId;
        }
    }

    public readonly struct PerformanceTierChangedEvent
    {
        public readonly PerformanceTier Tier;

        public PerformanceTierChangedEvent(PerformanceTier tier)
        {
            Tier = tier;
        }
    }
}
