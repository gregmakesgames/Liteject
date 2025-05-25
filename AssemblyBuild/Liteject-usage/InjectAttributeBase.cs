using JetBrains.Annotations;

namespace Liteject
{
    [MeansImplicitUse(ImplicitUseKindFlags.Assign)]
    public abstract class InjectAttributeBase : Liteject.Internal.PreserveAttribute
    {
        public bool Optional
        {
            get;
            set;
        }

        public object Id
        {
            get;
            set;
        }

        public InjectSources Source
        {
            get;
            set;
        }
    }
}
