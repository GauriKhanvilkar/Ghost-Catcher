namespace GhostCatcher
{
    public static class Constants
    {
        // Photon Player Custom Properties
        public const string PLAYER_ROLE      = "role";
        public const string PLAYER_IS_READY  = "isReady";

        // Photon Room Custom Properties
        public const string ROOM_GAME_STARTED   = "gameStarted";
        public const string ROOM_SABOTAGE_COUNT = "sabotageCount";
        public const string ROOM_GAME_PHASE     = "gamePhase";

        // Roles
        public const string ROLE_GHOST   = "Ghost";
        public const string ROLE_HUNTER  = "Hunter";

        // Game Phases
        public const string PHASE_LOBBY = "Lobby";
        public const string PHASE_GAME  = "Game";
        public const string PHASE_END   = "End";

        // Scene Names
        public const string SCENE_MENU  = "MainMenu";
        public const string SCENE_LOBBY = "Lobby";
        public const string SCENE_GAME  = "GamePlay";

        // Tags
        public const string TAG_PLAYER          = "Player";
        public const string TAG_SABOTAGE_OBJECT = "SabotageObject";
        public const string TAG_PROP             = "Prop";

        // Game Settings
        public const float GAME_DURATION         = 180f; // 3 minutes
        public const int   SABOTAGES_TO_WIN      = 3;
        public const int   MIN_PLAYERS_TO_START  = 2;    // lower for testing, set to 3 for production
        public const float SCAN_COOLDOWN         = 10f;
        public const float BLEND_DURATION        = 5f;
        public const float BLEND_COOLDOWN        = 15f;
        public const float BLEND_RANGE           = 2f;   // tiles
        public const float INTERACT_RANGE        = 1.2f;
        public const float SABOTAGE_HOLD_TIME    = 1f;
        public const float REPAIR_HOLD_TIME      = 2f;
    }
}
