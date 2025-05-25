using System.Collections.Generic;
using Liteject;

namespace Liteject
{
    public interface ICompositeInstaller<out T> : IInstaller where T : IInstaller
    {
        IReadOnlyList<T> LeafInstallers { get; }
    }
}