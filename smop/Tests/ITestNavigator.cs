using System;
using System.Windows.Controls;

namespace Smop.Tests
{
    public enum Test
    {
        Threshold,
        OdorProduction,
    }

    public class PageDoneEventArgs : EventArgs
    {
        public bool CanContinue { get; private set; }
        public object? Data { get; private set; }
        public PageDoneEventArgs(bool canContinue, object? data = null)
        {
            CanContinue = canContinue;
            Data = data;
        }
    }

    public interface ITestNavigator : IDisposable
    {
        event EventHandler<PageDoneEventArgs> PageDone;
        string Name { get; }
        bool HasStarted { get; }
        Page Start();
        Page NextPage(object? param);
        void Interrupt();
        void Emulate(EmulationCommand command, params object[] args);
    }
}
