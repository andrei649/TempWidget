using System;
using System.Threading;

namespace TempWidget
{
    /// <summary>
    /// Coordinates the background sensor polling thread and guarantees deterministic
    /// disposal of the active <see cref="ISensorSource"/> (<c>LhmSource</c> / <c>Computer.Close()</c>)
    /// both on normal worker exit and when worker join times out during application shutdown.
    /// </summary>
    internal sealed class SensorWorker : IDisposable
    {
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly object _gate = new object();
        private Thread _thread;
        private ISensorSource _activeSource;
        private volatile bool _stopping;
        private int _disposed;

        public bool Stopping => _stopping;

        public void Start(ThreadStart workerLoop, string name = "sensors", ThreadPriority priority = ThreadPriority.BelowNormal)
        {
            if (workerLoop == null) throw new ArgumentNullException(nameof(workerLoop));
            lock (_gate)
            {
                if (_stopping) return;
                _thread = new Thread(workerLoop) { IsBackground = true, Priority = priority, Name = name };
                _thread.Start();
            }
        }

        public void Wake()
        {
            if (_stopping) return;
            try { _wake.Set(); }
            catch (ObjectDisposedException) { }
        }

        public void WaitNext(int timeoutMs)
        {
            if (_stopping) return;
            try { _wake.WaitOne(timeoutMs); }
            catch (ObjectDisposedException) { }
        }

        public ISensorSource ReplaceSource(Func<ISensorSource> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            ReleaseCurrentSource();
            if (_stopping) return null;

            ISensorSource created = factory();
            if (created == null) return null;

            bool keep = false;
            lock (_gate)
            {
                if (!_stopping)
                {
                    _activeSource = created;
                    keep = true;
                }
            }
            if (!keep)
            {
                SafeDispose(created);
                return null;
            }
            return created;
        }

        public void ReleaseCurrentSource()
        {
            ISensorSource current;
            lock (_gate)
            {
                current = _activeSource;
                _activeSource = null;
            }
            SafeDispose(current);
        }

        public bool Stop(int joinTimeoutMs = 3000)
        {
            _stopping = true;
            try { _wake.Set(); }
            catch (ObjectDisposedException) { }

            Thread worker;
            lock (_gate) { worker = _thread; }

            bool joined = true;
            try
            {
                if (worker != null && worker != Thread.CurrentThread && worker.IsAlive)
                    joined = worker.Join(Math.Max(0, joinTimeoutMs));
            }
            catch (ThreadStateException) { }
            finally
            {
                ReleaseCurrentSource();
            }

            if (joined && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                try { _wake.Dispose(); }
                catch (ObjectDisposedException) { }
            }
            return joined;
        }

        public void Dispose()
        {
            Stop(3000);
        }

        private static void SafeDispose(IDisposable resource)
        {
            if (resource == null) return;
            try { resource.Dispose(); }
            catch (Exception ex) { Program.Log(ex); }
        }
    }
}
