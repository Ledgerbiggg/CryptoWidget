using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CryptoWidget.Common.Logger;
using CryptoWidget.Shell.ViewModels;

namespace CryptoWidget.Shell.Views;

/// <summary>设置窗口：币种增删/拖拽排序、显示开关、开机自启、代理、字体样式；ViewModel 单例保持编辑状态</summary>
public partial class SettingsWindow : Window
{
    // ---- 币种拖拽排序状态 ----
    private CoinEditItem? _dragItem;
    private ListBoxItem? _dragSourceContainer;
    private ListBoxItem? _dropTargetContainer;
    private bool _insertAfter;
    private Point _dragStartPoint;
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // 点「保存设置」：弹保存成功提示并自动关闭窗口
        vm.Saved += (_, _) => OnSaved();
        // 关闭前兜底保存：防止焦点转移未触发 LostFocus 导致小数位/代理改动丢失（静默，不弹提示）
        Closing += (_, _) => vm.SaveOnClose();

        // 窗口图标与主卡片一致（用户提供的比特币图标）。注意 .ico 必须用 IconBitmapDecoder 解码，BitmapImage 不支持
        try
        {
            var decoder = new IconBitmapDecoder(
                new Uri("pack://application:,,,/Assets/btc.ico", UriKind.Absolute),
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            Icon = decoder.Frames[0];
        }
        catch (Exception ex)
        {
            LoggerHelper.Error("设置窗口图标加载失败（已忽略）", ex);
        }
    }

    /// <summary>保存成功：提示后自动关闭设置窗口</summary>
    private void OnSaved()
    {
        MessageBox.Show(this, "配置已保存", "CryptoWidget", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    #region 币种拖拽排序

    private void CoinsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _dragItem = GetCoinFromEvent(sender as ListBox, e.OriginalSource as DependencyObject);
    }

    /// <summary>拖拽开始：源项半透明反馈，进入 DoDragDrop</summary>
    private void CoinsList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragItem == null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            ResetDragVisual();
            return;
        }
        var diff = e.GetPosition(null) - _dragStartPoint;
        if (Math.Abs(diff.X) <= 4 && Math.Abs(diff.Y) <= 4) return;

        var lb = sender as ListBox;
        if (lb == null) return;
        _dragSourceContainer = GetContainer(lb, _dragItem);
        if (_dragSourceContainer != null)
            _dragSourceContainer.Opacity = 0.4;
        DragDrop.DoDragDrop(lb, _dragItem, DragDropEffects.Move);
        ResetDragVisual();
    }

    /// <summary>拖拽悬停：鼠标在目标项上半区插入到前、下半区插入到后，并高亮目标项</summary>
    private void CoinsList_DragOver(object sender, DragEventArgs e)
    {
        var lb = sender as ListBox;
        var target = GetCoinFromEvent(lb, e.OriginalSource as DependencyObject);
        var container = target == null ? null : GetContainer(lb!, target);
        if (container == null)
        {
            ClearDropHighlight();
            return;
        }
        var pos = e.GetPosition(container);
        _insertAfter = pos.Y > container.ActualHeight / 2;
        if (_dropTargetContainer != container)
        {
            ClearDropHighlight();
            _dropTargetContainer = container;
            container.Background = new SolidColorBrush(Color.FromArgb(40, 0, 120, 212));
        }
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void CoinsList_Drop(object sender, DragEventArgs e)
    {
        var dragged = _dragItem;
        var lb = sender as ListBox;
        var target = GetCoinFromEvent(lb, e.OriginalSource as DependencyObject);
        ResetDragVisual();
        if (dragged != null && target != null && DataContext is SettingsViewModel vm)
            vm.MoveCoin(dragged, target, _insertAfter);
        e.Handled = true;
    }

    /// <summary>从鼠标命中的元素向上回溯到 ListBoxItem，取绑定的 CoinEditItem</summary>
    private static CoinEditItem? GetCoinFromEvent(ListBox? lb, DependencyObject? source)
    {
        if (lb == null || source == null) return null;
        var container = source;
        while (container != null && container is not ListBoxItem)
            container = VisualTreeHelper.GetParent(container);
        return container is ListBoxItem { Content: CoinEditItem ci } ? ci : null;
    }

    private static ListBoxItem? GetContainer(ListBox lb, CoinEditItem item)
    {
        foreach (var o in lb.Items)
        {
            if (o is CoinEditItem c && c.InstId == item.InstId)
                return lb.ItemContainerGenerator.ContainerFromItem(o) as ListBoxItem;
        }
        return null;
    }

    private void ResetDragVisual()
    {
        if (_dragSourceContainer != null)
            _dragSourceContainer.Opacity = 1;
        ClearDropHighlight();
        _dragItem = null;
        _dragSourceContainer = null;
    }

    private void ClearDropHighlight()
    {
        if (_dropTargetContainer != null)
            _dropTargetContainer.Background = null;
        _dropTargetContainer = null;
    }

    #endregion
}
