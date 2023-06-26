using System;

namespace kakunvr.FacialLockGenerator.Scripts
{
    public sealed class EditorException : Exception
    {
        public EditorException(string message) : base(message)
        {
        }
    }
}