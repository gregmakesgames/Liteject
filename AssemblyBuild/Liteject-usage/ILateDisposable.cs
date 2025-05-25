using System;

namespace Liteject
{
    public interface ILateDisposable
    {
        void LateDispose();
    }
}
