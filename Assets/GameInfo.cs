namespace GasHunter
{
    /// <summary>
    /// Build metadata kept in the predefined assembly so tooling that compiles
    /// against Assembly-CSharp (for example the MCP dynamic code bridge) keeps working.
    /// </summary>
    public static class GameInfo
    {
        public const string Title = "Gas Hunter";
        public const string Version = "0.1.0";
    }
}
