namespace _Project.Features.LobbySystem.Structs
{
    public enum MatchState
    {
        Lobby,           // Players are joining and picking teams
        CharacterSelect, // 3-minute countdown to lock in Proxy Characters
        LoadingMap,      // Server is loading the stage, clients are transitioning
        InProgress       // Players are spawned and playing
    }
}