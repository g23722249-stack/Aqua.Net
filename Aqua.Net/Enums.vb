Option Strict On
Option Explicit On

' Public enumerations ported verbatim from the VB6 Aqua library (CoClass\AquaRun.cls).
' Numeric values are preserved so behaviour and any persisted values stay identical.
Namespace Global.Aqua

    ''' <summary>Accent colour family used by scrollbars, check marks, etc. (VB6: Aqua.ColorConstants).</summary>
    Public Enum ColorConstants
        Blue = 0
        Brown = 1
        Coffee = 2
        Cyan = 3
        Gray = 4
        Green = 5
        Indigo = 6
        Metal = 7
        Olive = 8
        Orange = 9
        Pink = 10
        Red = 11
        Violet = 12
    End Enum

    ''' <summary>Which corners of a control get the rounded/obtuse region (VB6: Aqua.ObtusenessMode).</summary>
    ''' <remarks>Chinese identifiers in the VB6 enum are mapped to ASCII names here; numeric values match.</remarks>
    Public Enum ObtusenessMode
        All = 0          ' 四角
        LeftTop = 1      ' 左上
        LeftBottom = 2   ' 左下
        RightTop = 3     ' 右上
        RightBottom = 4  ' 右下
        Top = 5          ' 上方
        Bottom = 6       ' 下方
        Left = 7         ' 左方
        Right = 8        ' 右方
        None = 9         ' 無
    End Enum

    ''' <summary>Cell check-mark rendering style (VB6: Aqua.ItemCheckStyle).</summary>
    Public Enum ItemCheckStyle
        None = -1
        Check = 0
        Radio = 1
        Hook = 2
    End Enum

    ''' <summary>Column sort direction (VB6: Aqua.SortOrder).</summary>
    Public Enum SortOrder
        None = 0
        Ascending = 1
        Descending = 2
    End Enum

    ''' <summary>Grid selection granularity (VB6: Aqua.GridSelectionMode).</summary>
    Public Enum GridSelectionMode
        Cell = 0
        Row = 1
    End Enum

    ''' <summary>Scrollbar/updown orientation (VB6: Aqua.OrientationMode). 0 = vertical, 1 = horizontal.</summary>
    Public Enum OrientationMode
        Vertical = 0
        Horizontal = 1
    End Enum

    ''' <summary>How an ImageButton fits its state image to the control (VB6: Aqua.ImageSizeMode).</summary>
    Public Enum ImageSizeMode
        Normal = 0
        StretchImage = 1
        FastStretchImage = 2
        AutoSize = 3
        CenterImage = 4
        HorizontalStretch = 5
        VerticalStretch = 6
        Fill = 7
        Appose = 8
        Border = 9
    End Enum

    ''' <summary>UpDown button coupling (VB6: Aqua.UpDownStyle).</summary>
    Public Enum UpDownStyle
        Alignment = 0
        Independence = 1
    End Enum

    ''' <summary>Which edge the tabs sit on (VB6: Aqua.PageStyle). 0 Top, 1 Bottom, 2 Left, 3 Right.</summary>
    Public Enum PageStyle
        Top = 0
        Bottom = 1
        Left = 2
        Right = 3
    End Enum

    ''' <summary>
    ''' Horizontal alignment, keeping the VB6 VBRUN.AlignmentConstants numeric values
    ''' (Left=0, Right=1, Center=2) so the ported Cell layout logic is unchanged.
    ''' </summary>
    Public Enum AlignmentConstants
        LeftJustify = 0
        RightJustify = 1
        Center = 2
    End Enum

    ''' <summary>Label click behaviour (VB6: Aqua.LabelConduct, CoClass\AquaRun.cls).</summary>
    Public Enum LabelConduct
        Normal = 0   ' not clickable
        Header = 1   ' toggles Selected on click and stays highlighted
        Button = 2   ' flashes like a button, then reverts
    End Enum

    ''' <summary>Label corner style (VB6: Aqua.LabelStyle).</summary>
    Public Enum LabelStyle
        Obtuseness = 0   ' rounded pill shape
        Rectangle = 1
    End Enum

    ''' <summary>Icon position relative to the text (VB6: Aqua.LabelIconAlignment).</summary>
    Public Enum LabelIconAlignment
        MiddleLeft = 0
        MiddleRight = 1
    End Enum

    ''' <summary>Window chrome sizability (VB6: Aqua.FormBorderStyle, CoClass\AquaRun.cls:
    ''' 固定視窗/大小可調整). Used by AquaForm/iForm.</summary>
    Public Enum FormBorderStyle
        Fixed = 0
        Sizable = 1
    End Enum

    ''' <summary>Icon position relative to the text within one Buttons segment (VB6:
    ''' Aqua.ButtonsIconAlignment, CoClass\AquaRun.cls: 上方/下方/左方/右方).</summary>
    Public Enum ButtonsIconAlignment
        Top = 0
        Bottom = 1
        Left = 2
        Right = 3
    End Enum

    ''' <summary>EditBox border chrome (VB6: Aqua.EditBoxBorderStyle, CoClass\AquaRun.cls).</summary>
    Public Enum EditBoxBorderStyle
        NoBorder = 0
        FixedSingle = 1
        Normal = 2      ' native WS_EX_CLIENTEDGE sunken edge
        Raised = 3      ' approximated with Fixed3D -- no direct .NET equivalent for the raw raised edge
        Bumped = 4      ' approximated with Fixed3D -- no direct .NET equivalent for the raw bumped edge
        Aqua = 5        ' the themed 3-line border + rounded corners + focus glow (BorderPainter/RegionUtil)
    End Enum

    ''' <summary>Character case enforcement while typing (VB6: Aqua.CaseType). Read/written but never
    ''' actually applied in the VB6 EditBox.ctl source; this port makes it functional.</summary>
    Public Enum CaseType
        NoCase = 0
        UpperCase = 1
        LowerCase = 2
        ProperCase = 3
    End Enum

    ''' <summary>Drives which characters EditBox accepts while typing (VB6: Aqua.EditBoxTextFormat).</summary>
    Public Enum EditBoxTextFormat
        NoFormat = 0
        NumericOnly = 1
        DateFormat = 2
        CustomFormat = 3
    End Enum

    ''' <summary>Panel background/chrome treatment (VB6: Aqua.PanelStyleMode, CoClass\AquaRun.cls).</summary>
    Public Enum PanelStyleMode
        Flat = 0
        Container = 1        ' can take focus; shows a themed border + Parhelia glow
        Simulation = 2        ' mimics the parent form/container's own background showing through
        DarkSinking = 3
        LightSinking = 4
        SmoothSinking = 5
        DarkGrid = 6
        LightGrid = 7
    End Enum

    ''' <summary>Slider tick-mark placement (VB6: Aqua.SliderTickMode).</summary>
    Public Enum SliderTickMode
        BottomRight = 0
        TopLeft = 1
        NoTicks = 2
    End Enum

    ''' <summary>Whether the TimeLine thumb can be dragged (VB6: Aqua.TimeLineStyleConstants).</summary>
    Public Enum TimeLineStyleConstants
        Adjustable = 0
        DisplayOnly = 1
    End Enum

    ''' <summary>Whether ListBox shows a vertical scrollbar only, or both (VB6: Aqua.ScrollBarMode).</summary>
    Public Enum ScrollBarMode
        VerticalOnly = 0
        Both = 2
    End Enum

    ''' <summary>Which drives DriveListBox lists (VB6: Aqua.DriveListBox.ListDriveMode).</summary>
    Public Enum ListDriveMode
        All = 0
        AllWithNetworkNeighborhood = 1
        FixedOnly = 2
        NetworkOnly = 3
        RemovableOnly = 4
        NetworkNeighborhoodOnly = 5
    End Enum

    ''' <summary>Per-keystroke character filter for MaskEdit (VB6: Aqua.EditAllowedKeys). Bit flags,
    ''' combinable; numeric values match the VB6 enum exactly so PropertyBag-era values round-trip.
    ''' The AllowDate/AllowTime/AllowMoney/AllowPhone members are pre-combined convenience presets,
    ''' also carried over as-is.</summary>
    <Flags>
    Public Enum EditAllowedKeys
        AllowAll = 0
        AllowNoSpaces = 1              ' 2^0
        AllowNoSingleQuotes = 2        ' 2^1
        AllowNoDoubleQuotes = 4        ' 2^2

        AllowUppercase = 8             ' 2^3
        AllowLowercase = 16            ' 2^4
        AllowNumbers = 32              ' 2^5

        AllowDecimal = 64              ' 2^6
        AllowNegative = 128            ' 2^7
        AllowSpaces = 256              ' 2^8
        AllowStars = 512               ' 2^9
        AllowPounds = 1024             ' 2^10
        AllowForwardSlash = 2048       ' 2^11
        AllowParenthesis = 4096        ' 2^12
        AllowDollarSigns = 8192        ' 2^13
        AllowColon = 16384             ' 2^14
        AllowAMPM = 32768              ' 2^15

        AllowDate = AllowNumbers Or AllowForwardSlash
        AllowTime = AllowNumbers Or AllowColon Or AllowSpaces Or AllowAMPM
        AllowMoney = AllowNumbers Or AllowDollarSigns Or AllowNegative Or AllowDecimal
        AllowPhone = AllowNumbers Or AllowParenthesis Or AllowSpaces Or AllowNegative Or AllowPounds
    End Enum

    ''' <summary>A MediaItem's star rating (VB6: Aqua.MediaItemRanking). None hides the rating strip.</summary>
    Public Enum MediaItemRanking
        None = -1        ' (隱藏評分)
        NoRating = 0     ' 無評價
        OneStar = 1      ' 一顆星
        TwoStars = 2     ' 兩顆星
        ThreeStars = 3   ' 三顆星
        FourStars = 4    ' 四顆星
        FiveStars = 5    ' 五顆星
    End Enum

    ''' <summary>Whether a MediaList shows its items' ratings (VB6: Aqua.RankingMode).</summary>
    Public Enum RankingMode
        Hidden = -1      ' 關閉
        Shown = 1        ' 開啟
    End Enum

    ''' <summary>Where a MediaItem's mark image sits relative to the photo (VB6: Aqua.MediaItemMarkPosition).</summary>
    Public Enum MediaItemMarkPosition
        SnapToPhoto = 0     ' inside the photo, flush with the aligned edge
        CenterToPhoto = 1   ' centred on the aligned edge (half outside the photo)
    End Enum

End Namespace
