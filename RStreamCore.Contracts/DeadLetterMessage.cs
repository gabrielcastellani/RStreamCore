namespace RStreamCore.Contracts
{
    public record DeadLetterMessage(
        string StreamKey,
        string EntryId,
        string EventType,
        string Payload,
        int Attempts,
        string LastError,
        DateTime DeadAt);
}
