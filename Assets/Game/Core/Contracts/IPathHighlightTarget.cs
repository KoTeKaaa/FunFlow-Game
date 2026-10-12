public enum PathHighlightKind { Loot, Hazard, Guard }

// Implemented by gameplay components; placing their prefabs needs no highlight wiring.
public interface IPathHighlightTarget
{
    PathHighlightKind HighlightKind { get; }
    bool CanHighlight { get; }
}
