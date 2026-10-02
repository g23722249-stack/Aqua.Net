Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Reflection

' Managed replacement for the menu-related bits of LibResource/LibConst: the dropdown popup's
' background/selected-row art and next-level arrow (frmResMenu), plus its pixel layout constants
' (LibConst's twips-per-pixel GetXxxTwips functions, pre-converted to plain pixel Integers here
' since this port has no ScaleMode/twips concept).
Namespace Global.Aqua

    Friend Module MenuResources

        Public Const IconSize As Integer = 16              ' gc_intMenuWidth / gc_intMenuHeight
        Public Const TopBottomPadding As Integer = 8        ' GetMenuTopBottomHeightTwips
        Public Const ItemInterval As Integer = 2            ' gc_intMenuInterval (separator gap)
        Public Const ItemSeparatorHeight As Integer = 8      ' GetSubMenuItemSeparateTwips
        Public Const ItemPadding As Integer = 4              ' GetSubMenuItemIntervalTwips
        Public Const DefaultMinWidth As Integer = 60         ' GetSubMenuDefaultWidthTwips
        Public Const DefaultMinHeight As Integer = 24        ' GetSubMenuDefaultHeightTwips
        Public Const MainMenuInterval As Integer = 22        ' gc_intMainMenuInterval (AquaForm's top menu bar)

        Private ReadOnly _cache As New Dictionary(Of String, Image)()
        Private ReadOnly _sync As New Object()

        Public Function GetApple() As Image
            Return Load("mnu_imgApple")
        End Function

        Public Function GetBackground() As Image
            Return Load("mnu_imgMenuBackground")
        End Function

        Public Function GetSelected() As Image
            Return Load("mnu_imgMenuSelected")
        End Function

        Public Function GetNextLevel(ByVal disabled As Boolean) As Image
            Return Load("mnu_imgMenuNextLevel_" & If(disabled, "01", "00"))
        End Function

        Private Function Load(ByVal key As String) As Image
            SyncLock _sync
                Dim img As Image = Nothing
                If _cache.TryGetValue(key, img) Then Return img
                Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aqua.MenuRes." & key & ".bmp")
                    If s Is Nothing Then Return Nothing
                    Using tmp As New Bitmap(s)
                        img = New Bitmap(tmp)
                    End Using
                End Using
                _cache(key) = img
                Return img
            End SyncLock
        End Function

    End Module

End Namespace
