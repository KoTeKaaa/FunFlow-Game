using System;
using System.Collections.Generic;
using UnityEngine;

namespace FunFlow.Presentation
{
    [CreateAssetMenu(menuName = "Game/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Title;
            [TextArea] public string Description;
            public Sprite Artwork;
            [Tooltip("Полный путь сцены, включённой в Build Settings")]
            public string ScenePath;
        }

        [SerializeField] private List<Entry> levels = new List<Entry>();
        public IReadOnlyList<Entry> Levels => levels;
    }
}
