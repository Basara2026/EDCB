using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EpgTimer
{
    /// <summary>
    /// タブの見出しの行の配置と、設定ボタンの歯車アイコン
    /// </summary>
    static class TabHeaderLayout
    {
        /// <summary>
        /// タブの見出しの行(TabPanel)を、TabControlの中央に表示するかどうかを切り替える。
        /// falseのときは、テンプレート既定の配置(左寄せ)に戻す
        /// </summary>
        public static void SetCentered(TabControl tabControl, bool centered)
        {
            if (tabControl.IsLoaded)
            {
                Apply(tabControl, centered);
            }
            else
            {
                //画面に表示される前はテンプレートがないので、表示された時点で反映する
                RoutedEventHandler handler = null;
                handler = (sender, e) =>
                {
                    tabControl.Loaded -= handler;
                    Apply(tabControl, centered);
                };
                tabControl.Loaded += handler;
            }
        }

        /// <summary>
        /// タブの見出しの行(TabPanel)を表示するかどうかを切り替える。内容は表示されたまま、見出しだけを隠せる
        /// </summary>
        public static void SetHeaderVisible(TabControl tabControl, bool visible)
        {
            if (tabControl.IsLoaded)
            {
                ApplyHeaderVisible(tabControl, visible);
            }
            else
            {
                RoutedEventHandler handler = null;
                handler = (sender, e) =>
                {
                    tabControl.Loaded -= handler;
                    ApplyHeaderVisible(tabControl, visible);
                };
                tabControl.Loaded += handler;
            }
        }

        private static void ApplyHeaderVisible(TabControl tabControl, bool visible)
        {
            tabControl.ApplyTemplate();
            TabPanel panel = FindTabPanel(tabControl, tabControl);
            if (panel != null)
            {
                panel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static void Apply(TabControl tabControl, bool centered)
        {
            tabControl.ApplyTemplate();
            TabPanel panel = FindTabPanel(tabControl, tabControl);
            if (panel == null)
            {
                return;
            }
            if (centered)
            {
                if (panel.HorizontalAlignment != HorizontalAlignment.Center)
                {
                    panel.HorizontalAlignment = HorizontalAlignment.Center;
                }
            }
            else if (panel.ReadLocalValue(FrameworkElement.HorizontalAlignmentProperty) != DependencyProperty.UnsetValue)
            {
                panel.ClearValue(FrameworkElement.HorizontalAlignmentProperty);
            }
        }

        private static TabPanel FindTabPanel(DependencyObject parent, TabControl owner)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                //中のタブの内容にあるTabControlのTabPanelは対象外にする
                var panel = child as TabPanel;
                if (panel != null && ReferenceEquals(panel.TemplatedParent, owner))
                {
                    return panel;
                }
                panel = FindTabPanel(child, owner);
                if (panel != null)
                {
                    return panel;
                }
            }
            return null;
        }

        private const string GearIconBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAB4AAAAeCAYAAAA7MK6iAAAACXBIWXMAAAsTAAALEwEAmpwYAAAE8WlUWHRYTUw6Y29tLmFk" +
            "b2JlLnhtcAAAAAAAPD94cGFja2V0IGJlZ2luPSLvu78iIGlkPSJXNU0wTXBDZWhpSHpyZVN6TlRjemtjOWQiPz4gPHg6eG1w" +
            "bWV0YSB4bWxuczp4PSJhZG9iZTpuczptZXRhLyIgeDp4bXB0az0iQWRvYmUgWE1QIENvcmUgMTAuMC1jMDAwIDc5LjQzMzJh" +
            "YWUsIDIwMjYvMDgvMTctMTM6Mzk6NDMgICAgICAgICI+IDxyZGY6UkRGIHhtbG5zOnJkZj0iaHR0cDovL3d3dy53My5vcmcv" +
            "MTk5OS8wMi8yMi1yZGYtc3ludGF4LW5zIyI+IDxyZGY6RGVzY3JpcHRpb24gcmRmOmFib3V0PSIiIHhtbG5zOnhtcD0iaHR0" +
            "cDovL25zLmFkb2JlLmNvbS94YXAvMS4wLyIgeG1sbnM6ZGM9Imh0dHA6Ly9wdXJsLm9yZy9kYy9lbGVtZW50cy8xLjEvIiB4" +
            "bWxuczpwaG90b3Nob3A9Imh0dHA6Ly9ucy5hZG9iZS5jb20vcGhvdG9zaG9wLzEuMC8iIHhtbG5zOnhtcE1NPSJodHRwOi8v" +
            "bnMuYWRvYmUuY29tL3hhcC8xLjAvbW0vIiB4bWxuczpzdEV2dD0iaHR0cDovL25zLmFkb2JlLmNvbS94YXAvMS4wL3NUeXBl" +
            "L1Jlc291cmNlRXZlbnQjIiB4bXA6Q3JlYXRvclRvb2w9IkFkb2JlIFBob3Rvc2hvcCAyNy4xMSAoV2luZG93cykiIHhtcDpD" +
            "cmVhdGVEYXRlPSIyMDI2LTEwLTE4VDE3OjExOjIzKzA5OjAwIiB4bXA6TW9kaWZ5RGF0ZT0iMjAyNi0xMC0xOFQxOTo0Njo1" +
            "OSswOTowMCIgeG1wOk1ldGFkYXRhRGF0ZT0iMjAyNi0xMC0xOFQxOTo0Njo1OSswOTowMCIgZGM6Zm9ybWF0PSJpbWFnZS9w" +
            "bmciIHBob3Rvc2hvcDpDb2xvck1vZGU9IjMiIHhtcE1NOkluc3RhbmNlSUQ9InhtcC5paWQ6OTAyZjk4ZTItMDY4MS1jZDRl" +
            "LWFmZTEtYjM2OWVmODY4YWY4IiB4bXBNTTpEb2N1bWVudElEPSJ4bXAuZGlkOjkwMmY5OGUyLTA2ODEtY2Q0ZS1hZmUxLWIz" +
            "NjllZjg2OGFmOCIgeG1wTU06T3JpZ2luYWxEb2N1bWVudElEPSJ4bXAuZGlkOjkwMmY5OGUyLTA2ODEtY2Q0ZS1hZmUxLWIz" +
            "NjllZjg2OGFmOCI+IDx4bXBNTTpIaXN0b3J5PiA8cmRmOlNlcT4gPHJkZjpsaSBzdEV2dDphY3Rpb249ImNyZWF0ZWQiIHN0" +
            "RXZ0Omluc3RhbmNlSUQ9InhtcC5paWQ6OTAyZjk4ZTItMDY4MS1jZDRlLWFmZTEtYjM2OWVmODY4YWY4IiBzdEV2dDp3aGVu" +
            "PSIyMDI2LTEwLTE4VDE3OjExOjIzKzA5OjAwIiBzdEV2dDpzb2Z0d2FyZUFnZW50PSJBZG9iZSBQaG90b3Nob3AgMjcuMTEg" +
            "KFdpbmRvd3MpIi8+IDwvcmRmOlNlcT4gPC94bXBNTTpIaXN0b3J5PiA8L3JkZjpEZXNjcmlwdGlvbj4gPC9yZGY6UkRGPiA8" +
            "L3g6eG1wbWV0YT4gPD94cGFja2V0IGVuZD0iciI/Pj0cfwoAAAUHSURBVEjHrZdLaJxVFMdvS6YBFULNkzw6SIVkF8mDvN/v" +
            "92syeSHK0IVuAhYXCuJOKOhCsbixbiy2CsbWagW7qoIktZSuRJBii0qtolVoqLYDGX//y73D188vyRC6OHPvd+8593/Ouedx" +
            "x4yNjRnRxMSEpa6uLjM4OGgSiYRd1zg+Pm48H/TC6Ojob4w/QTegP5E7Njk5uQ+yvDpnZGTEDA8Pm6GhITv3Z3kyuwHPz8+b" +
            "mZkZA5gl1j6BMp5Y03gVmbyBgQEj8Kmpqb0B9/f3a/OQ1pPJpJmdnTV9fX2WOOgUfFlgN1/v7u6OdXZ2WiWnp6c98GH4H90V" +
            "WNq2t7dL4ATCf2NNygNrX3wcsoY7w8Df9Pb2WuvE6yxOIK8zPpPlOwLLlQi/xYZ1IfMMCqzOzc1ZpbCml/WfnXstSQno956e" +
            "nmkBixfgBYDTfp/5h8zzdW1ZYGnn6DFAZGn27kQISfg96CPW7wfvNwguy+E9y3ic7y2vXMA7XywuLpYvLS0ZkWlra7PU0dFx" +
            "2Gn3v4O9EkFLwyRgyYf5NMcoOwejW1fJaExxcbGl8vJyufNZ3JHhTnYEyZWCiuDJV0tLS2MFBQVGZFpbWy1oVVWVSaVSiuKj" +
            "pEU6GEB7JQFizBbeeEPRXllZaUpKSkx9fb2xUaioa2xsNMvLy8ZF4DsIRB7kgknfd6E7QTeH+d21fSxQBV5dXV22HhhfbbSp" +
            "AtDS0qJU+GA7YPa+Q7lVZFpYa4SO8L2hDAgFkw+4LxVDOlv5LTwpYSfSQOBKdCpQN0JbwQMEqENIgzPwFOoQX0blIWQeITbe" +
            "FY/3igcWAZwUn68Fqoz6WIX5fZhPMX7Kxq2w29z399DjApWgB5ayqnTkbx5rFxWYXt4HKMb9xdp55qehk/C8JOHNCJAHSLlN" +
            "KX2+trbWNDU1WSs9sLylFGloaDBUr3EFUzidAnHhc/6uhK/lEKGbHFAv61yjyIK667FWo1Ccg2/lcN6POuCHHPJxE4CnfKcJ" +
            "tj65XusCZ14J/80cUvGadbVPdHcfmXAtlqsJkGeqq6uzKeEDxVuuiGXeD396uyIScP8/0v4Yk2856BKLl5nfCWssZbBqg718" +
            "WRiMUFkvYNUAxnPblNN78F1h/xLjZdbetoIcFCMdYswPNDc3JwQUBpfGFPkTjDG5Nggsl5Nqr0XVaRURzl5l/4BkhSGZrODC" +
            "woLVXBUM4DNR9+QKwkX4ksyfYO0Q4wRrn0c1F9d0vibw9qsD+keClLY/elnIhf65g8CbUU3Cp4Zr/jehX/zaDvwnBapCVVNT" +
            "Y9PRAvNkMXqyqFWpT+KWp9Hy34fRJFRMALyPpS+qJ1RUVJiysjITj8dNFlQK0JkmccW9h9ESg8VICgCcUgcsLCw0RUVFxvBk" +
            "sUTVeTLKymDt3U2h7fh8sNKA2ghe24iC7+USBC5EvTzQ+DyCFyKeM+F8/QpaC9+5i+4rZEV19ukTANal57u3lY/IDPfzuqqS" +
            "AgKBZdZ/DUaw61y3mT9HgO5XZiBz1PM4JaT0Qb1YlT2iMLAv/udgTCP8sl6Gylvtr6ysaP9shJs3VKt1ZeJzfwCOME8zrqtO" +
            "SCEBe4oERvCgGr0OE7BvBKpYzE8HH/SyiPV1uleeglQAimCNpFEr+3EpniuwHd0/igdaIONa+JHA91XGPCnm3t/Z+i3ZPQHL" +
            "YhUA/xeH9Vc45A/9YWN+HQ/cBuw4Vu6TpZLNBfg/+kLp0UAvlyYAAAAASUVORK5CYII=";

        private static ImageSource gearIcon = null;

        /// <summary>
        /// 設定ボタンの歯車アイコンの画像を作る
        /// </summary>
        public static Image CreateGearImage()
        {
            if (gearIcon == null)
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(Convert.FromBase64String(GearIconBase64));
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                gearIcon = bitmap;
            }
            var image = new Image()
            {
                Source = gearIcon,
                Width = 24,
                Height = 24,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            return image;
        }
    }
}
