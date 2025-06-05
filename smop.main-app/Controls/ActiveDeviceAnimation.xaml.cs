using System.Linq;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Smop.MainApp.Controls;

public partial class ActiveDeviceAnimation : UserControl
{
    public ActiveDeviceAnimation()
    {
        InitializeComponent();

        _anim = new[] {
            FindResource("ML2OD") as Storyboard,
            FindResource("OD2ENose") as Storyboard,
            FindResource("ENose2ML") as Storyboard,
        }.Select(anim => anim!)
        .ToArray();
    }

    public void Init()
    {
        var initAnim = FindResource("Initial") as Storyboard;
        initAnim?.Begin(this);
        initAnim?.Stop(this);

        Storyboard? animPulsing = FindResource("Pulsing") as Storyboard;
        animPulsing?.Begin();
    }

    public void Next()
    {
        _anim[_animIndex].Stop();
        _anim[_animIndex++].Begin();
        if (_animIndex == _anim.Length)
            _animIndex = 0;
    }

    // Internal

    readonly Storyboard[] _anim;
    int _animIndex = 0;
}
