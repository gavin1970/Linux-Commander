using System;
using System.Threading;

internal class ConsoleSpinner : IDisposable
{
    private readonly string Sequence = @"/-\|";
    private bool _disposed = false; 
    private int _counter = 0;
    private Thread _thread = null;
    private EventWaitHandle _eventStop = new EventWaitHandle(false, EventResetMode.AutoReset);

    public TimeSpan MaxSpinTime { get; set; } = TimeSpan.FromDays(1);
    public bool HideCursor { get; set; } = true;
    public int SpinnerSpeed_ms { get; set; } = 100;
    public ConsoleColor SpinColor { get; set; } = ConsoleColor.Yellow;
    public ConsoleSpinner() {}

    public ConsoleSpinner(TimeSpan maxSpinTime)
    {
        MaxSpinTime = maxSpinTime;
    }

    ~ConsoleSpinner()
    {
        Dispose();
    }

    public void Dispose() => Dispose(true);

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        else
            _disposed = true;

        if (disposing)
            Stop();
    }

    public void Stop()
    {
        _eventStop.Set();
    }

    public void Start()
    {
        _thread = new Thread(() => Turn());
        _thread.Start();
    }

    private void Draw(char c)
    {
        Console.Write(c);
        Console.SetCursorPosition(Console.CursorLeft-1, Console.CursorTop);
    }

    private void Turn()
    {
        if(HideCursor)
            Console.CursorVisible = false;

        ConsoleColor prevColor = Console.ForegroundColor;
        Console.ForegroundColor = SpinColor;
        DateTime started = DateTime.UtcNow;

        while (!_eventStop.WaitOne(TimeSpan.FromMilliseconds(SpinnerSpeed_ms)) && DateTime.UtcNow.Subtract(started) < MaxSpinTime) {
            Draw(Sequence[++_counter % Sequence.Length]);
        }

        Console.ForegroundColor = prevColor;

        if (HideCursor)
            Console.CursorVisible = true;
    }
}
