using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Interfaces;

namespace Hidano.FacialControl.Adapters.Bone
{
    /// <summary>
    /// 1 件の <see cref="GazeBindingConfig"/> と、それを駆動する <see cref="IAnalogInputSource"/> のペア。
    /// <see cref="GazeBonePoseProvider"/> へ渡すための値オブジェクトで、入力源解決の責務を呼出側に閉じ込める。
    /// </summary>
    /// <remarks>
    /// <para>
    /// InputSystem 経路では <c>InputSystemGazeBinding.inputActionRef.action.name</c> を sourceId として
    /// 事前に解決済みの <see cref="IAnalogInputSource"/> をペアにする。OSC・ARKit 経路では
    /// それぞれの sourceId 体系で解決する。本構造体は入力方式に依存しない。
    /// </para>
    /// </remarks>
    public readonly struct GazeBoneBinding
    {
        public GazeBindingConfig Config { get; }
        public IAnalogInputSource Source { get; }

        public GazeBoneBinding(GazeBindingConfig config, IAnalogInputSource source)
        {
            Config = config;
            Source = source;
        }
    }
}
