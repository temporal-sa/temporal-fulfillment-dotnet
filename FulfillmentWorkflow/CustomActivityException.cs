namespace TemporalioSamples.ActivitySimple;

public class CustomActivityException : Exception
{
    public CustomActivityException()
        : base()
    {
    }

    public CustomActivityException(string message)
        : base(message)
    {
    }

    public CustomActivityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}