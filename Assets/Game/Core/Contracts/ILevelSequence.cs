public interface ILevelSequence
{
    // 1-based number of the level in the open scene.
    int LevelNumber { get; }
    int LevelCount { get; }
    bool HasNextLevel { get; }

    // Sum of the latest victory value of every level in this play session.
    int TotalValue { get; }

    // 1-based; 0 when the level has not been won yet.
    int GetLevelValue(int levelNumber);

    // Accepted only after a victory on a level that has a next one.
    void LoadNextLevel();

    // Clears the session results and opens the first level.
    void RestartFromFirstLevel();

    // Clears the session results and opens the main menu.
    void ReturnToMainMenu();
}
