using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ReFanS
{
    public class MusicCandidate
    {
        public string DisplayTitle { get; set; } = string.Empty;
        public string DisplaySub { get; set; } = string.Empty;
        public JsonElement RawData { get; set; }
    }
    public partial class MainWindow : Window
    {
        public ObservableCollection<ReferenceItem> ReferenceList { get; set; } = new ObservableCollection<ReferenceItem>();
        public ObservableCollection<FolderNode> FolderList { get; set; } = new ObservableCollection<FolderNode>();
        public ObservableCollection<FilterTreeNode> FilterTreeNodes { get; set; } = new ObservableCollection<FilterTreeNode>();

        // Library Menu Items ( Default : JA )
        public ObservableCollection<string> LibraryMenuItems { get; set; } = new ObservableCollection<string>
        {
            "📂  すべて表示 (All)", "⭐  ブックマーク (Bookmarks)", "📥  未分類", "🏷️  タグなし",
            "🎵  キュレーションカード (Curation Card)", "🔊  音楽 & オーディオ (Music & Audio)",
            "🌐  Web URL / リンク", "🖼️  画像 / スプライト", "🎬  動画 / GIF",
            "🔤  フォント (.ttf)", "🧊  3D & デザイン", "📄  ドキュメント (.txt/.md/.pdf)",
            "🎲  ランダム", "🗑️  ゴミ箱"
        };

        // ICON Presets (Emoji) for Folder & Filter TreeView ( 10x10 Grid )
        public List<string> EmojiPresets { get; } = new List<string>
        {
            "📁", "📂", "🗂️", "📊", "📌", "📍", "🏷️", "🔖", "⭐", "🌟",
            "🎵", "🎶", "🎼", "🎹", "🎷", "🎺", "🎸", "🎻", "🥁", "🎤",
            "🎧", "💿", "📀", "📻", "🎙️", "🎚️", "🎛️", "🔊", "🔔", "📢",
            "🎮", "🕹️", "👾", "🎲", "🀄", "🃏", "🎯", "🏆", "🥇", "👑",
            "🖼️", "🎨", "🖌️", "🖍️", "✏️", "✒️", "📐", "📏", "✂️", "📷",
            "🎬", "📽️", "🎞️", "📹", "📺", "🎭", "🎪", "🎫", "✨", "🌈",
            "📄", "📝", "📑", "📓", "📔", "📕", "📗", "📘", "📙", "📚",
            "💻", "🖥️", "⌨️", "🖱️", "💾", "🕹", "📱", "🔋", "🔌", "💡",
            "🧊", "💎", "🔮", "🧪", "🧬", "🔭", "🔬", "⚙️", "🔧", "🔨",
            "🔥", "⚡", "💧", "🌿", "🌸", "🍀", "🍎", "☕", "🚀", "❤️"
        };

        public ObservableCollection<LabelTag> MasterTagCatalog { get; set; } = new ObservableCollection<LabelTag>();
        public List<string> CustomFilterGroups { get; set; } = new List<string>();

        private BitmapSource? _rgbSpectrumBitmap;
        private Action<string>? _currentColorPickCallback;

        // Workspace Directory 
        private readonly string _workspaceConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace_config.txt");
        private string _workspaceRootDir = AppDomain.CurrentDomain.BaseDirectory;

        private string _dbFilePath => Path.Combine(_workspaceRootDir, "database.json");
        private string _folderFilePath => Path.Combine(_workspaceRootDir, "folders.json");
        private string _customMetaFilePath => Path.Combine(_workspaceRootDir, "custom_tags.json");
        private string _prefsFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_prefs.json");
        private string _currentTheme = "Dark"; // "Dark", "Gray", "White", "Purple"
        private string _imageFolderDir => Path.Combine(_workspaceRootDir, "Images");
        private string _iconFolderDir => Path.Combine(_workspaceRootDir, "Icons");
        private string _mediaFolderDir => Path.Combine(_workspaceRootDir, "Media");
        private static readonly HttpClient _httpClient = new HttpClient();

        private GridLength _savedRightColWidth = new GridLength(410);
        private GridLength _savedLeftColWidth = new GridLength(280);
        private bool _isLeftSidebarCollapsed = false;
        private bool _isRightSidebarCollapsed = true; 

        // Lerp Scroll Variables
        private ScrollViewer? _galleryScrollViewer;
        private double _targetVerticalOffset = 0;
        private double _currentVerticalOffset = 0;
        private bool _isLerpScrolling = false;
        
        private ReferenceItem? _hoveredDropTargetCard;
        private bool _hasMouseEnteredSearchPopup = false;
        private Point _treeDragStartPoint;

        private double _leftTargetVerticalOffset = 0;
        private double _leftCurrentVerticalOffset = 0;
        private bool _isLeftLerpScrolling = false;
        private bool _isLeftPanning = false;
        private Point _leftPanStartPoint;
        private double _leftPanStartOffset = 0;

        // Lightbox & Focus Edit Mode Variables
        private bool _isLightboxOpen = false;
        private bool _isRightDrawerSlidOut = false;
        private bool _isMediaPlaying = false;
        private bool _isUserSeekingMedia = false;
        private readonly DispatcherTimer _mediaTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };

        private DispatcherTimer? _gifTimer;
        private DispatcherTimer? _inlinePlayTimer;
        private MediaElement? _activeInlineMediaElement;
        private List<BitmapSource>? _gifFrames;
        private int _currentGifFrameIndex = 0;

        private object? _draggingTreeItem;
        private bool _isTreeDraggingActive = false;
        private System.Windows.Controls.Primitives.Popup? _runtimeGhostPopup;
        private TextBlock? _runtimeGhostText;

        private string _currentLang = "JA";
        private int _currentSortMode = 0;
        private FolderNode? _pendingNestTargetFolder;
        private FolderNode? _hoveredDropTargetFolder;

        private object? _draggingGalleryCard;
        private Point _galleryDragStartPt;
        private bool _isGalleryDraggingActive = false;

        private DispatcherTimer? _saveDebounceTimer;

        public MainWindow()
        {
            InitializeComponent();

            if (File.Exists(_workspaceConfigPath))
            {
                string savedDir = File.ReadAllText(_workspaceConfigPath).Trim();
                if (!string.IsNullOrEmpty(savedDir) && Directory.Exists(savedDir))
                    _workspaceRootDir = savedDir;
            }
            WorkspaceDirTextBox.Text = _workspaceRootDir;

            EnsureWorkspaceDirectories();

            EmojiPickerGrid.ItemsSource = EmojiPresets;
            ManagerTagListBox.ItemsSource = MasterTagCatalog;
            SmartFolderList.ItemsSource = LibraryMenuItems;

            Initialize100ColorPalette();
            InitializeRgbSpectrumMap();
            LoadDefaultFixedAppIcon();

            LoadCustomTagsCatalog();
            LoadFolders();
            LoadDatabase();

            GalleryList.ItemsSource = ReferenceList;
            UserFolderTree.ItemsSource = FolderList;
            FilterTreeView.ItemsSource = FilterTreeNodes;
            SmartFolderList.SelectedIndex = 0;

            RefreshTagCloud();

            if (ReferenceList.Any())
                GalleryList.SelectedIndex = 0;

            CompositionTarget.Rendering += OnCompositionTargetRendering;
            _mediaTimer.Tick += MediaTimer_Tick;

            LoadUserPreferences();

            this.Closing += (s, e) => FlushSaveAllDataImmediate();
        }

        // --- Right Upper Side Window Controls ---
        private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void MaximizeRestoreWindow_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                MaxRestoreBtn.Content = "\uE922"; // Maximize Unicode
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                MaxRestoreBtn.Content = "\uE923"; // Restore
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e) => this.Close();

        // --- Left Upper Option ( ICON Button )---
        private void LoadDefaultFixedAppIcon()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = new[]
                {
                    Path.Combine(baseDir, "icon.ico"),
                    Path.Combine(baseDir, "icon.png"),
                    Path.Combine(baseDir, "app.ico"),
                    Path.Combine(baseDir, "app.png"),
                    Path.Combine(_iconFolderDir, "app_logo.ico"),
                    Path.Combine(_iconFolderDir, "app_logo.png")
                };

                string? foundIcon = candidates.FirstOrDefault(File.Exists);
                if (!string.IsNullOrEmpty(foundIcon))
                {
                    BitmapImage bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(foundIcon, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();

                    this.Icon = bmp;
                    this.Dispatcher.InvokeAsync(() =>
                    {
                        var img = FindVisualChild<Image>(LeftSidebarBorder);
                        if (img != null && img.Name == "AppLogoImage")
                        {
                            img.Source = bmp;
                            var fallback = FindVisualChild<TextBlock>(LeftSidebarBorder);
                            if (fallback != null && fallback.Name == "AppLogoFallback")
                                fallback.Visibility = Visibility.Collapsed;
                        }
                    }, DispatcherPriority.Loaded);
                }
            }
            catch { }
        }

        // --- ToggleSidebar ---
        private void ToggleLeftSidebar_Click(object sender, RoutedEventArgs e)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            if (!_isLeftSidebarCollapsed)
            {
                if (LeftSidebarCol.Width.Value > 0) _savedLeftColWidth = LeftSidebarCol.Width;
                _isLeftSidebarCollapsed = true;
                ToggleLeftSidebarBtn.Content = "▶";
                HeaderExpandLeftBtn.Visibility = Visibility.Visible;

                var slideOut = new DoubleAnimation(0, -_savedLeftColWidth.Value, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease };
                slideOut.Completed += (s, args) =>
                {
                    if (_isLeftSidebarCollapsed)
                    {
                        LeftSidebarCol.MinWidth = 0;
                        LeftSidebarCol.Width = new GridLength(0);
                        LeftSplitterCol.Width = new GridLength(0);
                        LeftSidebarBorder.Visibility = Visibility.Collapsed;
                        LeftSplitter.Visibility = Visibility.Collapsed;
                    }
                };
                LeftDrawerTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
            }
            else
            {
                _isLeftSidebarCollapsed = false;
                ToggleLeftSidebarBtn.Content = "◀";
                HeaderExpandLeftBtn.Visibility = Visibility.Collapsed;

                LeftSidebarBorder.Visibility = Visibility.Visible;
                LeftSplitter.Visibility = Visibility.Visible;
                LeftSplitterCol.Width = new GridLength(5);
                LeftSidebarCol.MinWidth = 210;
                LeftSidebarCol.Width = _savedLeftColWidth;
                LeftDrawerTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-_savedLeftColWidth.Value, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
            }
        }

        private void ToggleRightSidebar_Click(object sender, RoutedEventArgs e)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            if (!_isRightSidebarCollapsed)
            {
                if (RightDetailCol.Width.Value > 0) _savedRightColWidth = RightDetailCol.Width;
                _isRightSidebarCollapsed = true;
                ToggleRightSidebarBtn.Content = "◀";

                var slideOut = new DoubleAnimation(0, _savedRightColWidth.Value, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease };
                slideOut.Completed += (s, args) =>
                {
                    if (_isRightSidebarCollapsed)
                    {
                        RightDetailCol.MinWidth = 0;
                        RightDetailCol.Width = new GridLength(0);
                        RightSplitterCol.Width = new GridLength(0);
                        RightDetailBorder.Visibility = Visibility.Collapsed;
                        RightSplitter.Visibility = Visibility.Collapsed;
                    }
                };
                RightDrawerTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
            }
            else
            {
                _isRightSidebarCollapsed = false;
                ToggleRightSidebarBtn.Content = "▶";
                if (_savedRightColWidth.Value < 330) _savedRightColWidth = new GridLength(410);

                RightDetailBorder.Visibility = Visibility.Visible;
                RightSplitter.Visibility = Visibility.Visible;
                RightSplitterCol.Width = new GridLength(5);
                RightDetailCol.MinWidth = 330;
                RightDetailCol.Width = _savedRightColWidth;
                RightDrawerTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(_savedRightColWidth.Value, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
            }
        }

        // --- In-Page Settings View & Basic Themes & Languages (JA/KO/EN) ---
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            if (InPageSettingsView.Visibility == Visibility.Visible)
            {
                CloseSettings_Click(sender, e);
                return;
            }

            WorkspaceDirTextBox.Text = _workspaceRootDir;
            GalleryContainer.Visibility = Visibility.Collapsed;
            LightboxView.Visibility = Visibility.Collapsed;
            FocusEditView.Visibility = Visibility.Collapsed;
            InPageSettingsView.Visibility = Visibility.Visible;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            InPageSettingsView.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
            SettingsViewScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
            SettingsViewScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
        }

        private void CloseSettings_Click(object sender, RoutedEventArgs e)
        {
            InPageSettingsView.Visibility = Visibility.Collapsed;
            GalleryContainer.Visibility = Visibility.Visible;
        }

        private void ApplyDefaultDarkTheme_Click(object sender, RoutedEventArgs e) => ApplyThemeInternal("Dark", false);
        private void ApplyEagleGrayTheme_Click(object sender, RoutedEventArgs e) => ApplyThemeInternal("Gray", false);
        private void ApplyWhiteTheme_Click(object sender, RoutedEventArgs e) => ApplyThemeInternal("White", false);
        private void ApplyPastelPurpleTheme_Click(object sender, RoutedEventArgs e) => ApplyThemeInternal("Purple", false);

        private void ApplyThemeInternal(string themeKey, bool showNotification)
        {
            _currentTheme = themeKey;

            switch (themeKey)
            {
                case "Gray":
                    this.Resources["ThemeCenterBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B2C30"));
                    this.Resources["ThemeSidebarBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#35363B"));
                    this.Resources["ThemeCardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#42444A"));
                    this.Resources["ThemeBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4C52"));
                    this.Resources["ThemeTextPrimary"] = new SolidColorBrush(Colors.White);
                    this.Resources["ThemeTextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
                    UpdateFolderAndFilterDefaultTextColors(isLightMode: false);
                    break;

                case "White":
                    this.Resources["ThemeCenterBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F8FA"));
                    this.Resources["ThemeSidebarBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                    this.Resources["ThemeCardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBEDF2"));
                    this.Resources["ThemeBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCDFE6"));
                    this.Resources["ThemeTextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E22"));
                    this.Resources["ThemeTextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#606266"));
                    // Black text for default folder/filter names in light mode, White text for dark mode
                    UpdateFolderAndFilterDefaultTextColors(isLightMode: true);
                    break;

                case "Purple":
                    this.Resources["ThemeCenterBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#242031"));
                    this.Resources["ThemeSidebarBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D283E"));
                    this.Resources["ThemeCardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B3452"));
                    this.Resources["ThemeBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4E456B"));
                    this.Resources["ThemeTextPrimary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4EEFF"));
                    this.Resources["ThemeTextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C3B5E0"));
                    UpdateFolderAndFilterDefaultTextColors(isLightMode: false);
                    break;

                default: // "Dark"
                    this.Resources["ThemeCenterBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));
                    this.Resources["ThemeSidebarBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252526"));
                    this.Resources["ThemeCardBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D30"));
                    this.Resources["ThemeBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333337"));
                    this.Resources["ThemeTextPrimary"] = new SolidColorBrush(Colors.White);
                    this.Resources["ThemeTextSecondary"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));
                    UpdateFolderAndFilterDefaultTextColors(isLightMode: false);
                    break;
            }

            SaveUserPreferences();
        }

        // Automatically update default text colors for folder and filter tree nodes based on the selected theme (light or dark mode)
        private void UpdateFolderAndFilterDefaultTextColors(bool isLightMode)
        {
            string targetNameCol = isLightMode ? "#1E1E22" : "#FFFFFF";
            string targetIconCol = isLightMode ? "#555555" : "#E0E0E0";

            static bool IsDefaultTextColor(string c) =>
                string.IsNullOrEmpty(c) ||
                c.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("#1E1E22", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("#E0E0E0", StringComparison.OrdinalIgnoreCase);

            static bool IsDefaultIconColor(string c) =>
                string.IsNullOrEmpty(c) ||
                c.Equals("#E0E0E0", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("#555555", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase);

            void UpdateFolderRecursive(IEnumerable<FolderNode> nodes)
            {
                foreach (var f in nodes)
                {
                    if (IsDefaultTextColor(f.NameColor)) f.NameColor = targetNameCol;
                    if (IsDefaultIconColor(f.IconColor)) f.IconColor = targetIconCol;
                    UpdateFolderRecursive(f.SubFolders);
                }
            }

            void UpdateFilterRecursive(IEnumerable<FilterTreeNode> nodes)
            {
                foreach (var n in nodes)
                {
                    if (IsDefaultTextColor(n.NameColor)) n.NameColor = targetNameCol;
                    if (IsDefaultIconColor(n.IconColor)) n.IconColor = targetIconCol;
                    UpdateFilterRecursive(n.SubNodes);
                }
            }

            if (FolderList != null) UpdateFolderRecursive(FolderList);
            if (FilterTreeNodes != null) UpdateFilterRecursive(FilterTreeNodes);
        }

        // Save & Load user preferences to user_prefs.json
        private void SaveUserPreferences()
        {
            try
            {
                string viewMode = ViewModeBtn?.Tag?.ToString() ?? "Curation";
                var prefs = new
                {
                    Language = _currentLang,
                    Theme = _currentTheme,
                    SortMode = _currentSortMode,
                    ViewMode = viewMode
                };
                File.WriteAllText(_prefsFilePath, JsonSerializer.Serialize(prefs));
            }
            catch { }
        }

        private void LoadUserPreferences()
        {
            string savedViewMode = "Curation";
            try
            {
                if (File.Exists(_prefsFilePath))
                {
                    using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(_prefsFilePath));
                    if (doc.RootElement.TryGetProperty("Theme", out var thElem))
                        _currentTheme = thElem.GetString() ?? "Dark";
                    if (doc.RootElement.TryGetProperty("Language", out var lgElem))
                        _currentLang = lgElem.GetString() ?? "JA";
                    if (doc.RootElement.TryGetProperty("SortMode", out var sortElem))
                        _currentSortMode = sortElem.GetInt32();
                    if (doc.RootElement.TryGetProperty("ViewMode", out var vmElem))
                        savedViewMode = vmElem.GetString() ?? "Curation";
                }
            }
            catch { }

            ViewModeBtn.Tag = savedViewMode;
            ApplyThemeInternal(_currentTheme, showNotification: false);
            ApplyLanguage(_currentLang);
            ApplySavedSortMode(_currentSortMode);
        }

        private void ApplySavedSortMode(int sortMode)
        {
            _currentSortMode = sortMode;
            UpdateSortButtonText();
            if (GalleryList?.ItemsSource == null) return;
            ICollectionView view = CollectionViewSource.GetDefaultView(GalleryList.ItemsSource);
            if (view == null) return;

            view.SortDescriptions.Clear();
            switch (sortMode)
            {
                case 0: break;
                case 1: view.SortDescriptions.Add(new SortDescription(nameof(ReferenceItem.CreatedAt), ListSortDirection.Descending)); break;
                case 2: view.SortDescriptions.Add(new SortDescription(nameof(ReferenceItem.Title), ListSortDirection.Ascending)); break;
                case 3:
                    view.SortDescriptions.Add(new SortDescription(nameof(ReferenceItem.ItemType), ListSortDirection.Ascending));
                    view.SortDescriptions.Add(new SortDescription(nameof(ReferenceItem.Title), ListSortDirection.Ascending));
                    break;
            }
            view.Refresh();
        }

        private void SetLangKo_Click(object sender, RoutedEventArgs e) => ApplyLanguage("KO");
        private void SetLangJa_Click(object sender, RoutedEventArgs e) => ApplyLanguage("JA");
        private void SetLangEn_Click(object sender, RoutedEventArgs e) => ApplyLanguage("EN");

        private void ApplyLanguage(string lang)
        {
            _currentLang = lang;
            var lblAddBtn = AddMenuBtn.Template?.FindName("LblAddBtn", AddMenuBtn) as TextBlock;

            static List<MenuItem> GetMenuItems(ContextMenu? cm) => cm?.Items.OfType<MenuItem>().ToList() ?? new List<MenuItem>();

            var sortMenus = GetMenuItems(SortMenuBtn.ContextMenu);
            var addMenus = GetMenuItems(AddMenuBtn.ContextMenu);
            var toolMenus = GetMenuItems(MoreToolsBtn.ContextMenu);
            var folderMenus = GetMenuItems(UserFolderTree.ContextMenu);
            var filterMenus = GetMenuItems(FilterTreeView.ContextMenu);

            // Left Top Buttons: [0]:Settings, [1]:Immediate Capture (🖥️), [2]:Minimize and Capture (📷), [3]:Area Capture (✂️), [4]:Collapse Sidebar (◧)
            var leftTopButtons = FindVisualChildren<Button>(LeftSidebarBorder).Take(5).ToList();
            var settingsButtons = FindVisualChildren<Button>(InPageSettingsView).ToList();
            var settingsTexts = FindVisualChildren<TextBlock>(InPageSettingsView).ToList();
            var searchPopupTitle = FindVisualChildren<TextBlock>(SearchFilterPopup.Child).FirstOrDefault();
            var paletteTexts = FindVisualChildren<TextBlock>(UniversalColorPickerPopup.Child).ToList();
            var rightToggleBtn = FindVisualChildren<Button>(GalleryContainer).FirstOrDefault(b => b.Content?.ToString() == "◨");

            var lbButtons = FindVisualChildren<Button>(LightboxView).Take(3).ToList();
            var focusButtons = FindVisualChildren<Button>(FocusEditView).ToList();
            var focusSubTitle = FindVisualChildren<TextBlock>(FocusEditView).FirstOrDefault(t => t.FontSize == 12 && t.Margin.Left == 15);

            var detailButtons = FindVisualChildren<Button>(DetailPanel).ToList();
            var detailTexts = FindVisualChildren<TextBlock>(DetailPanel).ToList();
            var tagSlashGuideText = detailTexts.FirstOrDefault(t => t.Text.StartsWith("TAGS"));
            var tagAddRightBtn = detailButtons.FirstOrDefault(b => Grid.GetColumn(b) == 2 && b.FontSize == 10.5);

            // Right Panel Buttons: [0]:Bookmark (⭐), [1]:Lock (🔒), [2]:Thumbnail from File (🖼️), [3]:Thumbnail from Clipboard
            // (📥), [4]:Copy Thumbnail to Clipboard (📋), [5]:Mini Browser (🌐), [6]:Undo Tag Remove (↩️), [7]:Tag Manager (⚙️)
            var bookmarkBtn = detailButtons.FirstOrDefault(b => b.Width == 28 && b.Margin.Right == 4 && b.Foreground.ToString().Contains("FFD700"));
            var lockItemBtn = detailButtons.FirstOrDefault(b => b.Width == 28 && b.Margin.Right == 6);
            var thumbFileBtn = detailButtons.FirstOrDefault(b => b.Content?.ToString() == "🖼️");
            var thumbClipBtn = detailButtons.FirstOrDefault(b => b.Content?.ToString() == "📥");
            var thumbCopyBtn = detailButtons.FirstOrDefault(b => b.Content?.ToString() == "📋");
            var miniBrowserBtn = detailButtons.FirstOrDefault(b => b.FontSize == 10 && b.Margin.Left == 4);
            var undoTagBtn = detailButtons.FirstOrDefault(b => b.Content?.ToString() == "↩️");
            var itemTagMgrBtn = detailButtons.FirstOrDefault(b => b.FontSize == 9.5 ||(b.Content?.ToString()?.Contains("⚙") == true));
            if (itemTagMgrBtn != null)
            {
                itemTagMgrBtn.Content = Loc("⚙️ タグ管理", "⚙️ 태그관리", "⚙️ Manage");
                itemTagMgrBtn.ToolTip = Loc("このファイルのタグを一括管理", "현재 파일의 태그 일괄 관리창 열기", "Manage Tags for This File");
            }

            var tmTexts = FindVisualChildren<TextBlock>(TagManagerModalOverlay).ToList();
            var tmButtons = FindVisualChildren<Button>(TagManagerModalOverlay).ToList();

            if (folderMenus.Count >= 2 && folderMenus[1].Items.Count > 0 && folderMenus[1].Items[0] is StackPanel emojiPanel)
            {
                var customImgBtn = emojiPanel.Children.OfType<Button>().FirstOrDefault();
                if (customImgBtn != null)
                    customImgBtn.Content = Loc("🖼️ + 画像からアイコン登録 (100x100)", "🖼️ + 내 이미지로 아이콘 등록 (100x100)", "🖼️ + Custom Image Icon (100x100)");
            }

            // 1. Basic Header, Buttons
            LblLibraryHeader.Text = Loc("ライブラリ", "LIBRARY", "LIBRARY");
            LblFoldersHeader.Text = Loc("フォルダ", "FOLDERS", "FOLDERS");
            LblTagsHeader.Text = Loc("タグ & フィルター", "TAGS & FILTER", "TAGS & FILTER");

            BtnAddFolder.Content = Loc("+ フォルダ", "+ 폴더", "+ Folder");
            BtnAddFolder.ToolTip = Loc("新しい最上位フォルダを追加", "새 최상위 폴더 추가", "Add New Root Folder");
            BtnAddFilterGroup.Content = Loc("+ グループ", "+ 그룹", "+ Group");
            BtnAddFilterGroup.ToolTip = Loc("カスタムフィルターグループを追加", "나만의 커스텀 필터 그룹 추가", "Add Custom Filter Group");
            BtnManageTags.Content = Loc("⚙️ 管理", "⚙️ 관리", "⚙️ Manage");
            BtnManageTags.ToolTip = Loc("タグ & フィルター統合マネージャーを開く", "태그 & 필터 통합 관리자 열기", "Open Tag & Filter Manager");

            LblSearchPlaceholder.Text = Loc("🔍 検索", "🔍 검색", "🔍 Search");
            if (lblAddBtn != null) lblAddBtn.Text = Loc("+ 追加", "+ 추가", "+ Add");
            MoreToolsBtn.Content = Loc("⋯ 特殊機能", "⋯ 특수기능", "⋯ Tools");
            MoreToolsBtn.ToolTip = Loc("特殊機能メニュー", "특수 기능 메뉴", "Special Tools Menu");
            LblInspectorHeader.Text = Loc("インスペクター", "QUICK INSPECTOR", "QUICK INSPECTOR");
            BtnSaveAll.Content = Loc("💾 保存 (自動保存有効)", "💾 저장 (자동저장 활성화됨)", "💾 Save All (Auto-Save On)");

            // 2. Right Panel
            if (tagSlashGuideText != null)
                tagSlashGuideText.Text = Loc("TAGS ('/' 入力でサブタグ作成)", "TAGS (슬래시 '/' 입력 시 하위 태그 생성)", "TAGS (Use '/' for Subtags)");
            if (tagAddRightBtn != null)
                tagAddRightBtn.Content = Loc("+ 追加", "+ 추가", "+ Add");
            if (bookmarkBtn != null)
                bookmarkBtn.ToolTip = Loc("ブックマークに追加 / 解除", "북마크 보관함에 추가 / 해제", "Bookmark / Unbookmark");
            if (lockItemBtn != null)
                lockItemBtn.ToolTip = Loc("ファイルをロック (編集・削除防止)", "파일 잠금 (수정 및 삭제 방지)", "Lock File (Prevent Edit/Delete)");
            if (thumbFileBtn != null)
                thumbFileBtn.ToolTip = Loc("ファイルからサムネイル変更", "파일에서 썸네일 변경", "Change Thumbnail from File");
            if (thumbClipBtn != null)
                thumbClipBtn.ToolTip = Loc("クリップボードの画像でサムネイル変更", "클립보드에 복사된 이미지로 썸네일 즉시 변경", "Paste Thumbnail from Clipboard");
            if (thumbCopyBtn != null)
                thumbCopyBtn.ToolTip = Loc("サムネイル画像をクリップボードにコピー", "현재 썸네일 이미지를 클립보드에 복사", "Copy Thumbnail to Clipboard");
            if (miniBrowserBtn != null)
                miniBrowserBtn.Content = Loc("🌐 ミニブラウザ", "🌐 미니 브라우저", "🌐 Mini Browser");
            if (undoTagBtn != null)
                undoTagBtn.ToolTip = Loc("直前に消したタグを復元 (Ctrl+Z)", "방금 지운 태그 복원 (Ctrl+Z)", "Undo Removed Tag (Ctrl+Z)");
            if (itemTagMgrBtn != null)
            {
                itemTagMgrBtn.Content = Loc("⚙️ タグ管理", "⚙️ 태그관리", "⚙️ Manage");
                itemTagMgrBtn.ToolTip = Loc("このファイルのタグを一括管理", "현재 파일의 태그 일괄 관리창 열기", "Manage Tags for This File");
            }

            // 3. Lightbox & Focus Edit Mode Buttons & Tags
            if (lbButtons.Count >= 3)
            {
                lbButtons[0].Content = Loc("← ギャラリーに戻る (ESC)", "← 갤러리 복귀 (ESC)", "← Back to Gallery (ESC)");
                lbButtons[1].Content = Loc("🚀 外部アプリで開く", "🚀 외부 프로그램 열기", "🚀 Open External App");
                lbButtons[2].Content = Loc("📝 集中編集モード (Space)", "📝 전체 화면 집중 편집 모드 (Space)", "📝 Full Focus Editor (Space)");
            }
            if (focusSubTitle != null)
                focusSubTitle.Text = Loc("集中編集ワークスペース", "집중 편집 워크스페이스", "Focus Edit Workspace");
            if (focusButtons.Count >= 6)
            {
                focusButtons[0].Content = Loc("← ギャラリーに戻る (ESC)", "← 갤러리로 돌아가기 (ESC)", "← Back to Gallery (ESC)");
                focusButtons[1].Content = Loc("🚀 外部実行", "🚀 외부 실행", "🚀 Open External");
                focusButtons[2].Content = Loc("🖼️ 画像変更", "🖼️ 이미지 변경", "🖼️ Change Image");
                focusButtons[3].Content = Loc("💾 保存", "💾 저장", "💾 Save");
                focusButtons[4].Content = Loc("🗑️ 削除", "🗑️ 삭제", "🗑️ Delete");
                focusButtons[5].Content = Loc("📋 元画像をコピー", "📋 원본 이미지 복사", "📋 Copy Original Image");
            }
            var tmTitleTxt = tmTexts.FirstOrDefault(t => t.FontSize == 17);
            var tmGuideTxt = tmTexts.FirstOrDefault(t => t.FontSize == 11);
            if (tmTitleTxt != null)
                tmTitleTxt.Text = Loc("🏷️ タグ & フィルター統合マネージャー", "🏷️ TAGS & FILTER 통합 관리자", "🏷️ Tag & Filter Manager");
            if (tmGuideTxt != null)
                tmGuideTxt.Text = Loc("✨ 新規タグ登録 (右の色ボタンで100色パレット & RGBマップを開きます)", "✨ 새 태그 등록 (우측의 색상 버튼을 누르면 10x10 팔레트 & RGB 맵이 열립니다)", "✨ Add Master Tag (Click color button for 100-Color & RGB Picker)");
            var tmAddBtn = tmButtons.FirstOrDefault(b => Grid.GetColumn(b) == 2 && b.Name != "ManagerColorPickerBtn");
            var tmDoneBtn = tmButtons.FirstOrDefault(b => b.Height == 34);
            if (tmAddBtn != null)
                tmAddBtn.Content = Loc("+ タグ登録", "+ 태그 등록", "+ Add Tag");
            if (tmDoneBtn != null)
                tmDoneBtn.Content = Loc("完了して閉じる", "완료 및 닫기", "Done & Close");

            // 4. Upper Side ( Sort, Add, Tools )
            if (sortMenus.Count >= 4)
            {
                sortMenus[0].Header = Loc("✋  手動並び替え (基本順序)", "✋  수동 정렬 (기본 순서)", "✋  Manual Order (Default)");
                sortMenus[1].Header = Loc("📅  登録日時順 (最新順)", "📅  날짜/시간순 (최신 등록순)", "📅  Date Added (Newest)");
                sortMenus[2].Header = Loc("🔤  名前順 (五十音 / ABC)", "🔤  이름순 (가나다 / ABC)", "🔤  Name (A to Z)");
                sortMenus[3].Header = Loc("📦  拡張子・ファイル種類別", "📦  확장자 및 파일 유형별", "📦  File Type & Extension");
            }
            if (addMenus.Count >= 4)
            {
                addMenus[0].Header = Loc("📂  ファイルをアップロード", "📂  파일 업로드 (탐색기 열기)", "📂  Upload Files from PC");
                addMenus[1].Header = Loc("🌐  Web URL / YouTube / SNSリンク追加", "🌐  웹 URL / YouTube / SNS 링크 추가", "🌐  Add Web URL / YouTube / SNS Link");
                addMenus[2].Header = Loc("🎵  キュレーションカード追加", "🎵  큐레이션 카드 추가", "🎵  New Curation Card");
                addMenus[3].Header = Loc("📄  新規ノート (.txt / .md) 追加", "📄  새 문서 (.txt / .md) 추가", "📄  New Document (.txt / .md)");
            }
            if (toolMenus.Count >= 8)
            {
                toolMenus[0].Header = Loc("📝 集中編集ワークスペースを開く", "📝 중앙 큰 편집 워크스페이스 열기", "📝 Open Full Focus Editor");
                toolMenus[1].Header = Loc("🚀 関連付けられた外部アプリで開く", "🚀 연결된 원본 파일/프로그램 실행", "🚀 Open in External Default App");
                toolMenus[2].Header = Loc("📂 エクスプローラーで場所を開く", "📂 탐색기에서 원본 파일 위치 열기", "📂 Reveal File in Explorer");
                toolMenus[3].Header = Loc("⚡ Apple Music 検索パネル表示/非表示", "⚡ 애플뮤직 자켓 검색창 열기/닫기", "⚡ Toggle Apple Music Search");
                toolMenus[4].Header = Loc("🔍 ドキュメント内の使用フォント再スキャン", "🔍 문서(.pdf/.psd) 사용 폰트 강제 재스캔", "🔍 Rescan Fonts in Document");
                toolMenus[5].Header = Loc("📸 SNSシェア用カード画像(PNG)出力", "📸 SNS 카드 이미지(PNG) 렌더링", "📸 Render SNS Share Card (PNG)");
                toolMenus[6].Header = Loc("📋 SNSハッシュタグテンプレートをコピー", "📋 SNS 해시태그 템플릿 복사", "📋 Copy SNS Hashtag Template");
                toolMenus[7].Header = Loc("📤 単一 JSON ファイルとして出力", "📤 단일 JSON 파일 추출", "📤 Export Single JSON");
            }

            // 5. Folder & Filter TreeView Context Menus
            if (folderMenus.Count >= 8)
            {
                folderMenus[0].Header = Loc("📁 サブフォルダ追加", "📁 하위 폴더 추가", "📁 Add Subfolder");
                folderMenus[1].Header = Loc("📤 ルート(最上位)へ移動", "📤 최상위 폴더로 빼기 (밖으로 이동)", "📤 Move to Root Level");
                folderMenus[2].Header = Loc("✨ フォルダアイコン変更", "✨ 폴더 아이콘(이모지/이미지) 변경", "✨ Change Folder Icon");
                folderMenus[3].Header = Loc("🎨 アイコン色変更 (100色 + RGB)", "🎨 폴더 아이콘 색상 변경 (100색 + RGB)", "🎨 Change Icon Color (100 + RGB)");
                folderMenus[4].Header = Loc("🖍️ フォルダ文字色変更 (100色 + RGB)", "🖍️ 폴더 이름(글자) 색상 변경 (100색 + RGB)", "🖍️ Change Text Color (100 + RGB)");
                folderMenus[5].Header = Loc("✏️ フォルダ名変更", "✏️ 폴더 이름 변경", "✏️ Rename Folder");
                folderMenus[6].Header = Loc("🔒 フォルダロック / ロック解除", "🔒 폴더 잠금 / 잠금 해제", "🔒 Lock / Unlock Folder");
                folderMenus[7].Header = Loc("🗑️ フォルダ削除", "🗑️ 폴더 삭제", "🗑️ Delete Folder");
            }
            if (filterMenus.Count >= 5)
            {
                filterMenus[0].Header = Loc("🏷️ 新規タグ / サブタグ追加", "🏷️ 새 태그 / 하위 태그 추가", "🏷️ Add New Tag / Subtag");
                filterMenus[1].Header = Loc("✏️ 名前変更 (一括反映)", "✏️ 이름 변경 (일괄 반영)", "✏️ Rename Tag (Global)");
                filterMenus[2].Header = Loc("🎨 タグ背景色変更 (100色 + RGB)", "🎨 태그/필터 배경 색상 변경 (100색 + RGB)", "🎨 Change Tag Color (100 + RGB)");
                filterMenus[3].Header = Loc("🖍️ タイトル文字色変更 (100色 + RGB)", "🖍️ 필터 제목(글자) 색상 변경 (100색 + RGB)", "🖍️ Change Header Color (100 + RGB)");
                filterMenus[4].Header = Loc("🗑️ このタグ/グループを削除", "🗑️ 이 태그/그룹 삭제", "🗑️ Delete Tag / Group");
            }

            // 6. Search Filter Popup
            if (searchPopupTitle != null) searchPopupTitle.Text = Loc("🎯 詳細検索フィルター", "🎯 상세 검색 필터", "🎯 Search Filters");
            ChkSearchCurrentFolder.Content = Loc("📁 現在のフォルダ内のみ検索", "📁 현재 선택된 폴더 내 검색", "📁 Search in Current Folder Only");
            ChkSearchTitleCreator.Content = Loc("🔤 タイトル / 作曲家 / 作品名", "🔤 제목 / 작곡가 / 작품명", "🔤 Title / Composer / Work");
            ChkSearchTags.Content = Loc("🏷️ タグ (Tags)", "🏷️ 태그 (Tags)", "🏷️ Tags");
            ChkSearchFileType.Content = Loc("📦 ファイル種類 / 拡張子", "📦 파일 종류 / 확장자", "📦 File Type / Extension");
            ChkSearchDate.Content = Loc("📅 登録日 (yyyy-MM-dd)", "📅 생성일 (yyyy-MM-dd)", "📅 Date Created (yyyy-MM-dd)");
            ChkSearchDocTextOnly.Content = Loc("📄 文書本文(.txt/.md)のみ検索", "📄 문서 본문(.txt/.md)만 검색", "📄 Document Body (.txt/.md) Only");

            if (paletteTexts.Count >= 1) paletteTexts[0].Text = Loc("🎨 100色パレット (基本 / パステル / グレートーン)", "🎨 100색 팔레트 (기본 / 파스텔 / 그레이톤)", "🎨 100 Colors (Vivid / Pastel / Gray Tone)");
            if (paletteTexts.Count >= 2) paletteTexts[1].Text = Loc("ドラッグしてRGBカラーを抽出", "마우스로 드래그하여 미세 RGB 색상을 추출하세요", "Drag to pick custom RGB color");

            // 7. Settings page
            if (settingsButtons.Count >= 1) settingsButtons[0].Content = Loc("← ギャラリーに戻る (ESC)", "← 갤러리로 돌아가기 (ESC)", "← Back to Gallery (ESC)");
            if (BtnCustomThemeColor != null) BtnCustomThemeColor.Content = Loc("🎨 カスタムテーマ色選択", "🎨 커스텀 테마 색상 선택", "🎨 Custom Theme Color");
            if (BtnCustomBgImage != null) BtnCustomBgImage.Content = Loc("🖼️ 背景画像を選択", "🖼️ 배경 사진 선택", "🖼️ Select Wallpaper");
            if (BtnClearBgImage != null) BtnClearBgImage.Content = Loc("リセット", "초기화", "Reset");

            var opacityLabel = settingsTexts.FirstOrDefault(t => t.Text.Contains("투명도") || t.Text.Contains("透明度") || t.Text.Contains("Opacity"));
            if (opacityLabel != null) opacityLabel.Text = Loc("透明度:", "투명도:", "Opacity:");

            var driveChangeBtn = settingsButtons.FirstOrDefault(b => b.Margin.Left == 6 && b.Padding.Left == 14);
            var driveOpenBtn = settingsButtons.FirstOrDefault(b => b.Margin.Left == 6 && b.Padding.Left == 12);
            if (driveChangeBtn != null) driveChangeBtn.Content = Loc("📁 フォルダ変更", "📁 폴더 변경", "📁 Change Folder");
            if (driveOpenBtn != null) driveOpenBtn.Content = Loc("エクスプローラーで開く", "탐색기 열기", "Open Explorer");

            LblSettingsTitle.Text = Loc("⚙️ 環境設定 (Preferences)", "⚙️ 환경 설정 (Preferences)", "⚙️ Preferences & Cloud Sync");
            LblLangSection.Text = Loc("🌐 言語設定 (Language)", "🌐 언어 설정 (Language)", "🌐 Language");
            LblThemeSection.Text = Loc("🎨 外観テーマ (Appearance Theme)", "🎨 스킨 및 외관 테마 (Appearance Theme)", "🎨 Appearance Theme");
            LblStorageSection.Text = Loc("📂 保存先フォルダ & ☁️ Google Drive 連携", "📂 데이터 저장 폴더 (Workspace Directory) & ☁️ Google Drive 연동", "📂 Workspace Directory & ☁️ Google Drive Sync");

            var driveUploadAllBtn = settingsButtons.FirstOrDefault(b => b.FontSize == 11.5 && b.Margin.Right == 4 && b.Margin.Left == 0);
            var driveUploadSelBtn = settingsButtons.FirstOrDefault(b => b.FontSize == 11.5 && b.Margin.Left == 4 && b.Margin.Right == 4);
            var driveExportZipBtn = settingsButtons.FirstOrDefault(b => b.FontSize == 11.5 && b.Margin.Left == 4 && b.Margin.Right == 0);

            if (driveUploadAllBtn != null)
                driveUploadAllBtn.Content = Loc("☁️ 全体を Google Drive へアップロード", "☁️ 전체 폴더 Google Drive 업로드", "☁️ Upload All to Google Drive");
            if (driveUploadSelBtn != null)
                driveUploadSelBtn.Content = Loc("📁 選択フォルダのみ Drive アップロード", "📁 선택 폴더만 Drive 업로드", "📁 Upload Selected Folder to Drive");
            if (driveExportZipBtn != null)
                driveExportZipBtn.Content = Loc("🌐 ZIP抽出 & Web Driveを開く", "🌐 ZIP 추출 & 웹 드라이브 열기", "🌐 Export ZIP & Open Web Drive");

            // 8. Left Upper Buttons (Settings, Capture, Sidebar)
            if (leftTopButtons.Count >= 5)
            {
                leftTopButtons[0].ToolTip = Loc("環境設定 (Preferences)", "환경 설정 (Preferences)", "Preferences");
                leftTopButtons[1].ToolTip = Loc("全画面キャプチャ (最小化なし)", "전체화면캡쳐", "Capture Full Screen (Immediate)");
                leftTopButtons[2].ToolTip = Loc("最小化後に全画面キャプチャ", "최소화 후 전체화면 캡쳐", "Minimize & Capture Full Screen");
                leftTopButtons[3].ToolTip = Loc("ドラッグで範囲指定キャプチャ", "마우스 드래그로 영역 지정 캡처", "Capture Selected Region");
                leftTopButtons[4].ToolTip = Loc("左サイドバーを折りたたむ", "좌측 사이드바 접기", "Collapse Left Sidebar");
            }
            HeaderExpandLeftBtn.ToolTip = Loc("左サイドバーを開く", "좌측 사이드바 펴기", "Expand Left Sidebar");
            if (rightToggleBtn != null) rightToggleBtn.ToolTip = Loc("右インスペクターを開く / 閉じる", "우측 인스펙터 사이드바 열기 / 닫기", "Open / Close Right Inspector");
            ToggleLeftSidebarBtn.ToolTip = Loc("左サイドバーを開く / 閉じる", "좌측 사이드바 접기 / 펴기", "Open / Close Left Sidebar");
            ToggleRightSidebarBtn.ToolTip = Loc("右サイドバーを開く / 閉じる", "우측 사이드바 접기 / 펴기", "Open / Close Right Sidebar");
            SortMenuBtn.ToolTip = Loc("並び替えオプション", "정렬 방식 변경", "Sort Options");
            ViewModeBtn.ToolTip = Loc("表示モード切替", "뷰 모드 전환", "Switch View Mode");
            AddMenuBtn.ToolTip = Loc("新しいリファレンスを追加", "새 레퍼런스 추가", "Add New Reference");

            // 9. Library Menu Items
            var libs = lang == "JA"
                ? new[] { "📂  すべて表示 (All)", "⭐  ブックマーク (Bookmarks)", "📥  未分類", "🏷️  タグなし", "🎵  キュレーションカード (Curation Card)", "🔊  音楽 & オーディオ (Music & Audio)", "🌐  Web URL / リンク", "🖼️  画像 / スプライト", "🎬  動画 / GIF", "🔤  フォント (.ttf)", "🧊  3D & デザイン", "📄  ドキュメント (.txt/.md/.pdf)", "🎲  ランダム", "🗑️  ゴミ箱" }
                : lang == "EN"
                ? new[] { "📂  All References", "⭐  Bookmarks", "📥  Uncategorized", "🏷️  Untagged", "🎵  Curation Card", "🔊  Music & Audio", "🌐  Web URL / Links", "🖼️  Images & Sprites", "🎬  Videos & GIFs", "🔤  Fonts (.ttf/.otf)", "🧊  3D & Design & Tools", "📄  Docs & Markdown", "🎲  Random Pick", "🗑️  Trash" }
                : new[] { "📂  전부 보기 (All)", "⭐  북마크 보관함 (Bookmarks)", "📥  분류되지 않음", "🏷️  태그 없음", "🎵  큐레이션 카드 (Curation Card)", "🔊  음악 & 오디오 (Music & Audio)", "🌐  웹 URL / 링크", "🖼️  이미지 / 스프라이트", "🎬  비디오 / 움짤 (.mp4/.gif)", "🔤  폰트 (.ttf / .otf)", "🧊  3D & 디자인 & 유틸", "📄  문서 & 코드 (.txt/.md/.pdf)", "🎲  랜덤 레퍼런스", "🗑️  휴지통 (Trash)" };

            while (LibraryMenuItems.Count < libs.Length) LibraryMenuItems.Add("");
            for (int i = 0; i < libs.Length; i++)
                LibraryMenuItems[i] = libs[i];

            UpdateSortButtonText();
            ViewModeBtn.Content = (ViewModeBtn.Tag?.ToString() == "ImageBoard")
                ? Loc("🎵 キュレーション", "🎵 큐레이션 뷰", "🎵 Curation View")
                : Loc("🖼️ ボード表示", "🖼️ 이미지 보드", "🖼️ Moodboard");

            SaveUserPreferences();
        }

        // --- User Directory & Google Drive  ---
        private void EnsureWorkspaceDirectories()
        {
            if (!Directory.Exists(_imageFolderDir)) Directory.CreateDirectory(_imageFolderDir);
            if (!Directory.Exists(_iconFolderDir)) Directory.CreateDirectory(_iconFolderDir);
            if (!Directory.Exists(_mediaFolderDir)) Directory.CreateDirectory(_mediaFolderDir);
        }

        private void ChangeWorkspaceDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Choose Your Workspace Directory",
                InitialDirectory = _workspaceRootDir
            };

            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.FolderName))
            {
                string newDir = dlg.FolderName;
                if (newDir.Equals(_workspaceRootDir, StringComparison.OrdinalIgnoreCase)) return;

                SaveAllData();
                SaveCustomTagsCatalog();

                string newDbPath = Path.Combine(newDir, "database.json");
                if (!File.Exists(newDbPath) && MessageBox.Show("Copy to New Folder?", "Change Workspace folder", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (File.Exists(_dbFilePath)) File.Copy(_dbFilePath, newDbPath, true);
                        if (File.Exists(_folderFilePath)) File.Copy(_folderFilePath, Path.Combine(newDir, "folders.json"), true);
                        if (File.Exists(_customMetaFilePath)) File.Copy(_customMetaFilePath, Path.Combine(newDir, "custom_tags.json"), true);
                    }
                    catch { }
                }

                _workspaceRootDir = newDir;
                File.WriteAllText(_workspaceConfigPath, _workspaceRootDir);
                WorkspaceDirTextBox.Text = _workspaceRootDir;
                EnsureWorkspaceDirectories();

                ReferenceList.Clear();
                FolderList.Clear();
                MasterTagCatalog.Clear();
                LoadCustomTagsCatalog();
                LoadFolders();
                LoadDatabase();
                UserFolderTree.ItemsSource = FolderList;
                RefreshTagCloud();

                ShowToast($"📂 Directory Changed to '{Path.GetFileName(newDir)}'");
            }
        }

        private void OpenWorkspaceDirectory_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(_workspaceRootDir))
                Process.Start(new ProcessStartInfo("explorer.exe", _workspaceRootDir) { UseShellExecute = true });
        }

        private void UploadAllToGoogleDrive_Click(object sender, RoutedEventArgs e)
        {
            SaveAllData();
            SaveCustomTagsCatalog();
            var dlg = new OpenFolderDialog { Title = "Choose Google Drive Folder to Upload (e.g., G:\\My Drive)" };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.FolderName))
            {
                try
                {
                    string targetDriveFolder = Path.Combine(dlg.FolderName, $"ReFanS_Sync_{DateTime.Now:yyyyMMdd}");
                    CopyDirectoryRecursive(_workspaceRootDir, targetDriveFolder);
                    ShowToast("☁️ Workplace Upload Complete");
                    Process.Start(new ProcessStartInfo("explorer.exe", targetDriveFolder) { UseShellExecute = true });
                }
                catch (Exception ex) { MessageBox.Show($"Upload Error: {ex.Message}"); }
            }
        }

        private void UploadSelectedFolderToGoogleDrive_Click(object sender, RoutedEventArgs e)
        {
            string selectedFolderName = UserFolderTree.SelectedItem is FolderNode fn ? fn.Name : "Default";
            var itemsInFolder = ReferenceList.Where(i => !i.IsDeleted && i.FolderName == selectedFolderName).ToList();

            var dlg = new OpenFolderDialog { Title = $"'{selectedFolderName}' Folder({itemsInFolder.Count}checked items) to upload to Google Drive" };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.FolderName))
            {
                try
                {
                    string safeName = string.Join("_", selectedFolderName.Split(Path.GetInvalidFileNameChars()));
                    string destDir = Path.Combine(dlg.FolderName, $"ReFanS_{safeName}");
                    Directory.CreateDirectory(destDir);

                    var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                    File.WriteAllText(Path.Combine(destDir, $"{safeName}_metadata.json"), JsonSerializer.Serialize(itemsInFolder, options));

                    foreach (var item in itemsInFolder)
                    {
                        if (!string.IsNullOrEmpty(item.CoverImagePath) && File.Exists(item.CoverImagePath))
                            File.Copy(item.CoverImagePath, Path.Combine(destDir, Path.GetFileName(item.CoverImagePath)), true);
                        if (!string.IsNullOrEmpty(item.MediaFilePath) && File.Exists(item.MediaFilePath))
                            File.Copy(item.MediaFilePath, Path.Combine(destDir, Path.GetFileName(item.MediaFilePath)), true);
                    }

                    ShowToast($"☁️ '{selectedFolderName}' Google Drive Upload Complete");
                    Process.Start(new ProcessStartInfo("explorer.exe", destDir) { UseShellExecute = true });
                }
                catch (Exception ex) { MessageBox.Show($"Upload Error: {ex.Message}"); }
            }
        }

        private void ExportZipAndOpenGoogleDriveWeb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveAllData();
                SaveCustomTagsCatalog();
                string zipName = $"ReFanS_CloudBackup_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
                string zipPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), zipName);

                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(_workspaceRootDir, zipPath);

                Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"/select,\"{zipPath}\"" });
                Process.Start(new ProcessStartInfo("https://drive.google.com/drive/my-drive") { UseShellExecute = true });
                ShowToast("📦 Back up ZIP Created");
            }
            catch (Exception ex) { MessageBox.Show($"Upload Error: {ex.Message}"); }
        }

        private void CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            foreach (string subDir in Directory.GetDirectories(sourceDir))
                CopyDirectoryRecursive(subDir, Path.Combine(targetDir, Path.GetFileName(subDir)));
        }

        // --- WebView2 Browser ---
        private void OpenMiniBrowser_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item && !string.IsNullOrWhiteSpace(item.WebUrl))
            {
                Window webWin = new Window
                {
                    Width = 1100,
                    Height = 750,
                    Title = $"ReFanS Mini Browser - {item.Title}",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = new SolidColorBrush(Color.FromRgb(24, 24, 28))
                };

                var browser = new Microsoft.Web.WebView2.Wpf.WebView2();
                webWin.Content = browser;
                webWin.Loaded += async (s, ev) =>
                {
                    await browser.EnsureCoreWebView2Async();
                    browser.CoreWebView2.Navigate(item.WebUrl);
                };
                webWin.ShowDialog();
            }
            else
            {
                ShowToast("⚠️ Invalid Web URL");
            }
        }

        // --- Bookmark & Lock ---
        private void ToggleBookmarkItem_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item)
            {
                item.IsBookmarked = !item.IsBookmarked;
                SaveAllData();
                ShowToast(item.IsBookmarked
                    ? Loc("⭐ ブックマークに追加しました", "⭐ 북마크에 추가되었습니다.", "⭐ Added to Bookmarks")
                    : Loc("☆ ブックマークを解除しました", "☆ 북마크가 해제되었습니다.", "☆ Removed from Bookmarks"));
            }
        }

        private void ToggleLockItem_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item)
            {
                item.IsLocked = !item.IsLocked;
                SaveAllData();
                ShowToast(item.IsLocked
                    ? Loc("🔒 ファイルをロックしました (編集・削除防止)", "🔒 파일이 잠겼습니다 (수정/삭제 방지).", "🔒 File locked")
                    : Loc("🔓 ファイルのロックを解除しました", "🔓 파일 잠금이 해제되었습니다.", "🔓 File unlocked"));
            }
        }

        private void ToggleFolderLock_Click(object sender, RoutedEventArgs e)
        {
            if (UserFolderTree.SelectedItem is FolderNode folder)
            {
                folder.IsLocked = !folder.IsLocked;
                SaveFolders();
                ShowToast(folder.IsLocked
                    ? Loc($"🔒 '{folder.Name}' フォルダをロックしました", $"🔒 '{folder.Name}' 폴더 잠김", $"🔒 Locked '{folder.Name}'")
                    : Loc($"🔓 '{folder.Name}' フォルダのロックを解除しました", $"🔓 '{folder.Name}' 폴더 잠금 해제", $"🔓 Unlocked '{folder.Name}'"));
            }
        }

        // --- 10x10 Color Palette & RGB Map Picker ---
        private void Initialize100ColorPalette()
        {
            var palette100 = new List<string>();
            for (int row = 0; row < 3; row++)
            {
                double light = 0.45 + row * 0.10;
                for (int col = 0; col < 10; col++)
                {
                    if (col == 9)
                    {
                        byte g = (byte)(40 + row * 70);
                        palette100.Add($"#{g:X2}{g:X2}{g:X2}");
                    }
                    else
                    {
                        Color c = HslToRgb(col * 40.0, 0.85, light);
                        palette100.Add($"#{c.R:X2}{c.G:X2}{c.B:X2}");
                    }
                }
            }
            for (int row = 0; row < 3; row++)
            {
                double light = 0.78 + row * 0.05;
                double sat = 0.75 - row * 0.10;
                for (int col = 0; col < 10; col++)
                {
                    Color c = HslToRgb(col * 36.0, sat, light);
                    palette100.Add($"#{c.R:X2}{c.G:X2}{c.B:X2}");
                }
            }
            for (int row = 0; row < 4; row++)
            {
                double sat = 0.35 + (row % 2) * 0.15;
                double light = 0.42 + row * 0.08;
                for (int col = 0; col < 10; col++)
                {
                    Color c = HslToRgb(col * 36.0, sat, light);
                    palette100.Add($"#{c.R:X2}{c.G:X2}{c.B:X2}");
                }
            }
            Universal100ColorGrid.ItemsSource = palette100;
        }

        private void OpenUniversalColorPicker(UIElement placementTarget, Action<string> onColorSelected)
        {
            _currentColorPickCallback = onColorSelected;
            UniversalColorPickerPopup.PlacementTarget = placementTarget;
            UniversalColorPickerPopup.IsOpen = true;
        }

        private void OpenInspectorColorPicker_Click(object sender, RoutedEventArgs e)
        {
            OpenUniversalColorPicker(InspectorColorPickerBtn, hex =>
            {
                InspectorColorPickerBtn.Content = hex;
                InspectorColorPickerBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            });
        }

        private void OpenManagerColorPicker_Click(object sender, RoutedEventArgs e)
        {
            OpenUniversalColorPicker(ManagerColorPickerBtn, hex =>
            {
                ManagerColorPickerBtn.Content = hex;
                ManagerColorPickerBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            });
        }

        private void OpenColorPickerForFolderIcon_Click(object sender, RoutedEventArgs e)
        {
            if (UserFolderTree.SelectedItem is FolderNode folder)
            {
                OpenUniversalColorPicker(UserFolderTree, hex =>
                {
                    folder.IconColor = hex;
                    SaveFolders();
                    ShowToast($"🎨 '{folder.Name}' Icon Applied ({hex})");
                });
            }
        }

        private void OpenColorPickerForFolderName_Click(object sender, RoutedEventArgs e)
        {
            if (UserFolderTree.SelectedItem is FolderNode folder)
            {
                OpenUniversalColorPicker(UserFolderTree, hex =>
                {
                    folder.NameColor = hex;
                    SaveFolders();
                    ShowToast($"🖍️ '{folder.Name}' Text Color Applied ({hex})");
                });
            }
        }

        private void OpenColorPickerForFilterNode_Click(object sender, RoutedEventArgs e)
        {
            if (FilterTreeView.SelectedItem is FilterTreeNode node)
            {
                OpenUniversalColorPicker(FilterTreeView, hex =>
                {
                    node.HexColor = hex;
                    foreach (var m in MasterTagCatalog.Where(t => t.Name.Equals(node.FullMatchKey, StringComparison.OrdinalIgnoreCase) || t.Name.StartsWith(node.FullMatchKey + "/", StringComparison.OrdinalIgnoreCase)))
                        m.HexColor = hex;
                    foreach (var item in ReferenceList)
                        foreach (var l in item.Labels.Where(t => t.Name.Equals(node.FullMatchKey, StringComparison.OrdinalIgnoreCase) || t.Name.StartsWith(node.FullMatchKey + "/", StringComparison.OrdinalIgnoreCase)))
                            l.HexColor = hex;
                    SaveCustomTagsCatalog();
                    SaveAllData();
                    ShowToast($"🎨 '{node.DisplayName}' Color Changed ({hex})");
                });
            }
        }

        private void OpenColorPickerForFilterHeader_Click(object sender, RoutedEventArgs e)
        {
            if (FilterTreeView.SelectedItem is FilterTreeNode node)
            {
                OpenUniversalColorPicker(FilterTreeView, hex =>
                {
                    node.NameColor = hex;
                    ShowToast($"🖍️ '{node.DisplayName}' Text Color Changed ({hex})");
                });
            }
        }

        private void UniversalColorChip_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.DataContext is string hex)
            {
                _currentColorPickCallback?.Invoke(hex);
                UniversalColorPickerPopup.IsOpen = false;
            }
        }

        private void TogglePopupRgbMap_Click(object sender, RoutedEventArgs e)
        {
            PopupRgbMapBorder.Visibility = PopupRgbMapBorder.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        // -- RGB Spectrum Map Generation --
        private void InitializeRgbSpectrumMap()
        {
            int width = 260, height = 115;
            byte[] pixels = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                double normY = (double)y / height;
                double saturation = normY < 0.5 ? (0.25 + normY * 1.5) : 1.0;
                double lightness = normY < 0.5 ? (0.90 - normY * 0.8) : (0.50 - (normY - 0.5) * 0.75);
                for (int x = 0; x < width; x++)
                {
                    Color c = HslToRgb(((double)x / width) * 360.0, Math.Clamp(saturation, 0, 1), Math.Clamp(lightness, 0.1, 0.95));
                    int idx = (y * width + x) * 4;
                    pixels[idx] = c.B; pixels[idx + 1] = c.G; pixels[idx + 2] = c.R; pixels[idx + 3] = 255;
                }
            }
            _rgbSpectrumBitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
            RgbSpectrumImage.Source = _rgbSpectrumBitmap;
        }

        private static Color HslToRgb(double h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = l - c / 2.0;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromRgb((byte)Math.Clamp((r + m) * 255, 0, 255), (byte)Math.Clamp((g + m) * 255, 0, 255), (byte)Math.Clamp((b + m) * 255, 0, 255));
        }

        private void RgbSpectrumMap_MouseDownOrMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _rgbSpectrumBitmap == null || sender is not FrameworkElement fe) return;
            Point pt = e.GetPosition(fe);
            int x = (int)Math.Clamp((pt.X / fe.ActualWidth) * _rgbSpectrumBitmap.PixelWidth, 0, _rgbSpectrumBitmap.PixelWidth - 1);
            int y = (int)Math.Clamp((pt.Y / fe.ActualHeight) * _rgbSpectrumBitmap.PixelHeight, 0, _rgbSpectrumBitmap.PixelHeight - 1);
            byte[] pixel = new byte[4];
            _rgbSpectrumBitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
            _currentColorPickCallback?.Invoke($"#{pixel[2]:X2}{pixel[1]:X2}{pixel[0]:X2}");
        }

        // -- Drag & Drop Ghost Popup for Library / Folders / Tags --- ( Important for UX )
        private void EnsureDragGhostPopup()
        {
            if (_runtimeGhostPopup != null) return;
            _runtimeGhostText = new TextBlock { Foreground = Brushes.White, FontSize = 12.5, FontWeight = FontWeights.Bold };
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 0, 122, 204)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 6, 12, 6),
                Child = _runtimeGhostText
            };
            _runtimeGhostPopup = new System.Windows.Controls.Primitives.Popup
            {
                IsHitTestVisible = false,
                Placement = System.Windows.Controls.Primitives.PlacementMode.RelativePoint,
                PlacementTarget = this,
                AllowsTransparency = true,
                Child = border
            };
        }

        private void UpdateGhostPosition(Point winPt, string label)
        {
            EnsureDragGhostPopup();
            if (_runtimeGhostText != null) _runtimeGhostText.Text = label;
            if (_runtimeGhostPopup != null)
            {
                if (!_runtimeGhostPopup.IsOpen) _runtimeGhostPopup.IsOpen = true;
                _runtimeGhostPopup.HorizontalOffset = winPt.X + 14;
                _runtimeGhostPopup.VerticalOffset = winPt.Y - 12;
            }
        }

        private ReferenceItem? GetGalleryCardAtMouse(MouseEventArgs e)
        {
            foreach (var container in FindVisualChildren<ListBoxItem>(GalleryList))
            {
                if (!container.IsVisible || container.ActualWidth <= 0 || container.ActualHeight <= 0)
                    continue;

                Point pt = e.GetPosition(container);
                if (pt.X >= 0 && pt.Y >= 0 && pt.X <= container.ActualWidth && pt.Y <= container.ActualHeight)
                {
                    if (container.DataContext is ReferenceItem item)
                        return item;
                }
            }
            return null;
        }

        private void HideGhostPopup()
        {
            _isTreeDraggingActive = false;
            _draggingTreeItem = null;
            _pendingNestTargetFolder = null;
            _hoveredDropTargetCard = null;
            if (_runtimeGhostPopup != null) _runtimeGhostPopup.IsOpen = false;
        }

        // --- Leftside LIBRARY / FOLDERS / TAGS Drag & Drop ( Important for UX ) ---
        private void LibraryItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _treeDragStartPoint = e.GetPosition(this);
            var item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
            _draggingTreeItem = item?.DataContext as string;
            _isTreeDraggingActive = false;
        }
        private void LibraryItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { HideGhostPopup(); return; }
            if (_draggingTreeItem is not string sourceStr) return;

            Point curWinPt = e.GetPosition(this);
            if (!_isTreeDraggingActive && Math.Abs(curWinPt.Y - _treeDragStartPoint.Y) > 6)
                _isTreeDraggingActive = true;

            if (_isTreeDraggingActive)
            {
                UpdateGhostPosition(curWinPt, sourceStr);

                var hit = SmartFolderList.InputHitTest(e.GetPosition(SmartFolderList)) as DependencyObject;
                var targetContainer = FindAncestor<ListBoxItem>(hit);
                if (targetContainer?.DataContext is string targetStr && sourceStr != targetStr)
                {
                    int oldIdx = LibraryMenuItems.IndexOf(sourceStr);
                    int newIdx = LibraryMenuItems.IndexOf(targetStr);
                    if (oldIdx >= 0 && newIdx >= 0)
                    {
                        LibraryMenuItems.Move(oldIdx, newIdx);
                        SmartFolderList.SelectedItem = sourceStr;
                    }
                }
            }
        }

        private void LibraryItem_Drop(object sender, DragEventArgs e) => HideGhostPopup();

        private void FolderTree_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) { HideGhostPopup(); return; }
            if (_draggingTreeItem is not FolderNode sourceFolder) return;

            Point curWinPt = e.GetPosition(this);
            if (!_isTreeDraggingActive && Math.Abs(curWinPt.Y - _treeDragStartPoint.Y) > 6)
                _isTreeDraggingActive = true;

            if (_isTreeDraggingActive)
            {
                _pendingNestTargetFolder = null;
                var hit = UserFolderTree.InputHitTest(e.GetPosition(UserFolderTree)) as DependencyObject;
                var targetContainer = FindAncestor<TreeViewItem>(hit);

                if (targetContainer?.DataContext is FolderNode targetFolder && sourceFolder != targetFolder && !IsDescendantFolder(sourceFolder, targetFolder))
                {
                    // Calculate relative Y position of mouse within the target folder item to determine if it's in the top, middle, or bottom region
                    Point relPt = e.GetPosition(targetContainer);
                    double headerHeight = 28.0;
                    double ratioY = Math.Clamp(relPt.Y / headerHeight, 0.0, 1.0);

                    if (ratioY >= 0.25 && ratioY <= 0.75)
                    {
                        // Visual feedback for "nesting" the source folder into the target folder
                        _pendingNestTargetFolder = targetFolder;
                        string nestMsg = _currentLang == "JA" ? $"📥 '{targetFolder.Name}' のサブフォルダへ移動" :
                                         _currentLang == "EN" ? $"📥 Move inside '{targetFolder.Name}'" :
                                         $"📥 '{targetFolder.Name}' 하위 폴더로 넣기";
                        UpdateGhostPosition(curWinPt, nestMsg);
                        return;
                    }
                    else
                    {
                        // Change Line Order
                        var parentCol = FindParentCollection(FolderList, targetFolder);
                        var srcCol = FindParentCollection(FolderList, sourceFolder);
                        if (parentCol != null && parentCol == srcCol)
                        {
                            parentCol.Move(parentCol.IndexOf(sourceFolder), parentCol.IndexOf(targetFolder));
                            SaveFolders();
                        }
                    }
                }

                UpdateGhostPosition(curWinPt, $"{sourceFolder.Icon}  {sourceFolder.Name}");
            }
        }

        private void FolderTree_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // SubFolders Nesting Logic
            if (_isTreeDraggingActive && _draggingTreeItem is FolderNode sourceFolder && _pendingNestTargetFolder != null)
            {
                var targetFolder = _pendingNestTargetFolder;
                if (sourceFolder != targetFolder && !IsDescendantFolder(sourceFolder, targetFolder))
                {
                    RemoveFolderRecursive(FolderList, sourceFolder);
                    targetFolder.SubFolders.Add(sourceFolder);
                    targetFolder.IsExpanded = true;
                    SaveFolders();
                }
            }
            _pendingNestTargetFolder = null;
            HideGhostPopup();
        }

        private void FolderTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _treeDragStartPoint = e.GetPosition(this);
            var item = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
            _draggingTreeItem = item?.DataContext as FolderNode;
            _isTreeDraggingActive = false;
        }

        private bool IsDescendantFolder(FolderNode potentialParent, FolderNode target)
        {
            if (potentialParent.SubFolders.Contains(target)) return true;
            return potentialParent.SubFolders.Any(sub => IsDescendantFolder(sub, target));
        }

        private ObservableCollection<FolderNode>? FindParentCollection(ObservableCollection<FolderNode> currentList, FolderNode target)
        {
            if (currentList.Contains(target)) return currentList;
            foreach (var f in currentList)
            {
                var found = FindParentCollection(f.SubFolders, target);
                if (found != null) return found;
            }
            return null;
        }

        // --- Filter Tree Drag & Drop Logic ---

        private void FilterTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _treeDragStartPoint = e.GetPosition(this);
            var item = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
            _draggingTreeItem = item?.DataContext as FilterTreeNode;
            _isTreeDraggingActive = false;
            _hoveredDropTargetCard = null;
        }

        private void FilterTreeItem_Drop(object sender, DragEventArgs e) => HideGhostPopup();
        private void FilterTree_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                // Avoid CaptureMouse() being called when the mouse is released outside the tree area,
                // which would prevent the GhostPopup from disappearing when the mouse leaves the tree area.
                if (!_isTreeDraggingActive) HideGhostPopup();
                return;
            }
            if (_draggingTreeItem is not FilterTreeNode sourceNode) return;

            Point curWinPt = e.GetPosition(this);
            if (!_isTreeDraggingActive && (Math.Abs(curWinPt.X - _treeDragStartPoint.X) > 4 || Math.Abs(curWinPt.Y - _treeDragStartPoint.Y) > 4))
            {
                _isTreeDraggingActive = true;
                _hoveredDropTargetCard = null;
                FilterTreeView.CaptureMouse();
            }

            if (_isTreeDraggingActive)
            {
                // check if the mouse is over a gallery card to show the ghost popup for tag application
                var cardUnderMouse = GetGalleryCardAtMouse(e);
                if (cardUnderMouse != null)
                {
                    _hoveredDropTargetCard = cardUnderMouse;
                    UpdateGhostPosition(curWinPt, Loc($"🏷️ '{cardUnderMouse.Title}' に '{sourceNode.DisplayName}' 追加", $"🏷️ '{cardUnderMouse.Title}'에 '{sourceNode.DisplayName}' 태그 추가", $"🏷️ Add to '{cardUnderMouse.Title}'"));
                    return;
                }

                // reset the hovered drop target card if the mouse is not over any gallery card ( null )
                // 갤러리 영역 밖(좌측 트리 위)에 있을 때는 타겟 카드 초기화
                _hoveredDropTargetCard = null;
                UpdateGhostPosition(curWinPt, $"🏷️ {sourceNode.DisplayName}");

                var hit = FilterTreeView.InputHitTest(e.GetPosition(FilterTreeView)) as DependencyObject;
                var targetContainer = FindAncestor<TreeViewItem>(hit);
                if (targetContainer?.DataContext is FilterTreeNode targetNode && sourceNode != targetNode)
                {
                    if (FilterTreeNodes.Contains(sourceNode) && FilterTreeNodes.Contains(targetNode))
                    {
                        FilterTreeNodes.Move(FilterTreeNodes.IndexOf(sourceNode), FilterTreeNodes.IndexOf(targetNode));
                    }
                    else
                    {
                        foreach (var parent in FilterTreeNodes)
                        {
                            if (parent.SubNodes.Contains(sourceNode) && parent.SubNodes.Contains(targetNode))
                            {
                                parent.SubNodes.Move(parent.SubNodes.IndexOf(sourceNode), parent.SubNodes.IndexOf(targetNode));
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void TreeDrag_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Backup the current dragging state before resetting it, so we can use it to determine if we need to apply a tag to a gallery card.
            bool wasDragging = _isTreeDraggingActive;
            var draggedTagNode = _draggingTreeItem as FilterTreeNode;
            ReferenceItem? targetCard = _hoveredDropTargetCard ?? GetGalleryCardAtMouse(e);

            if (FilterTreeView.IsMouseCaptured) FilterTreeView.ReleaseMouseCapture();
            HideGhostPopup();

            if (wasDragging && draggedTagNode != null && !draggedTagNode.IsCategoryHeader && targetCard != null)
            {
                string tagKeyToApply = !string.IsNullOrWhiteSpace(draggedTagNode.FullMatchKey)
                    ? draggedTagNode.FullMatchKey
                    : draggedTagNode.DisplayName.Replace("🏷️", "").Trim();

                string tagHexToApply = !string.IsNullOrWhiteSpace(draggedTagNode.HexColor)
                    ? draggedTagNode.HexColor
                    : "#61AFEF";

                var targets = GalleryList.SelectedItems.Cast<ReferenceItem>().ToList();
                if (!targets.Contains(targetCard)) targets = new List<ReferenceItem> { targetCard };

                int addedCount = 0;
                foreach (var item in targets)
                {
                    if (!item.IsLocked && !item.Labels.Any(l => l.Name.Equals(tagKeyToApply, StringComparison.OrdinalIgnoreCase)))
                    {
                        item.Labels.Add(new LabelTag { Name = tagKeyToApply, HexColor = tagHexToApply });
                        addedCount++;
                    }
                }

                if (addedCount > 0)
                {
                    // If the tag is not already in the master tag catalog, add it automatically
                    if (!MasterTagCatalog.Any(m => m.Name.Equals(tagKeyToApply, StringComparison.OrdinalIgnoreCase)))
                    {
                        MasterTagCatalog.Add(new LabelTag { Name = tagKeyToApply, HexColor = tagHexToApply });
                        SaveCustomTagsCatalog();
                    }

                    SaveAllData();
                    RefreshTagCloud();
                    GalleryList.Items.Refresh();
                    ShowToast(Loc($"🏷️ {targets.Count}個のアイテムに '{tagKeyToApply}' タグを追加しました", $"🏷️ {targets.Count}개 파일에 '{tagKeyToApply}' 태그 적용 완료!", $"🏷️ Applied '{tagKeyToApply}' to {targets.Count} items!"));
                }
                else
                {
                    ShowToast(Loc($"ℹ️ 既に '{tagKeyToApply}' タグが付いています", $"ℹ️ 이미 '{tagKeyToApply}' 태그가 들어있습니다.", $"ℹ️ Tag '{tagKeyToApply}' is already applied."));
                }
            }
        }

        private void AddUrlReference_Click(object sender, RoutedEventArgs e)
        {
            string? inputUrl = PromptInput(
                Loc("追加する Web URL (YouTube / X / Instagram / Web) を入力してください:", "추가할 웹 URL (유튜브 / X / 인스타 / 웹페이지) 주소를 입력하세요:", "Enter Web URL (YouTube / X / Instagram / Webpage):"),
                Loc("Web URL 追加", "웹 URL 레퍼런스 추가", "Add Web URL Reference"),
                "https://");

            if (!string.IsNullOrWhiteSpace(inputUrl) && inputUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                string targetFolder = UserFolderTree.SelectedItem is FolderNode fn ? fn.Name : "Default";
                Uri.TryCreate(inputUrl.Trim(), UriKind.Absolute, out Uri? parsedUri);
                string hostName = parsedUri?.Host.Replace("www.", "") ?? "Web Link";

                string badgeCover = GenerateFileTypePlaceholderCover("URL", hostName.ToUpper(), "Web Reference", Color.FromRgb(86, 182, 194));

                var newItem = new ReferenceItem
                {
                    ItemType = "URL",
                    Title = hostName,
                    Creator = "Web / SNS Link",
                    WebUrl = inputUrl.Trim(),
                    CoverImagePath = badgeCover,
                    Resolution = hostName,
                    FolderName = targetFolder
                };
                ReferenceList.Insert(0, newItem);
                GalleryList.SelectedItem = newItem;
                SaveAllData();
                ShowToast("🌐 URL Apply Complete");
            }
        }

        // --- Filter Tree Double Click to Apply Filter ( Logic Changed for tag drag & drop ) ---
        private void FilterTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FilterTreeView.SelectedItem is FilterTreeNode clicked)
            {
                if (clicked.IsCategoryHeader) return;

                SetFocusEditMode(false);
                SetLightboxMode(false);
                CloseSettings_Click(sender, e);
                SmartFolderList.UnselectAll();

                ApplyFilter(item =>
                {
                    if (item.IsDeleted) return false;
                    return clicked.CategoryType switch
                    {
                        "Tag" => item.Labels.Any(l => l.Name.Equals(clicked.FullMatchKey, StringComparison.OrdinalIgnoreCase) || l.Name.StartsWith(clicked.FullMatchKey + "/", StringComparison.OrdinalIgnoreCase)),
                        "TagExact" => item.Labels.Any(l => l.Name.Equals(clicked.FullMatchKey, StringComparison.OrdinalIgnoreCase)),
                        "Composer" => item.Composer.Equals(clicked.FullMatchKey, StringComparison.OrdinalIgnoreCase),
                        "ComposerGame" => $"{item.Composer}|{item.GameTitle}".Equals(clicked.FullMatchKey, StringComparison.OrdinalIgnoreCase),
                        "Creator" => item.Creator.Equals(clicked.FullMatchKey, StringComparison.OrdinalIgnoreCase),
                        "Game" => item.GameTitle.Equals(clicked.FullMatchKey, StringComparison.OrdinalIgnoreCase),
                        "Company" => item.Company.Equals(clicked.FullMatchKey, StringComparison.OrdinalIgnoreCase),
                        "Color" => item.ColorPalette.Any(c => IsColorSimilar(c, clicked.HexColor, 65.0)),
                        "Font" => item.DetectedFonts.Any(f => f.Equals(clicked.FullMatchKey, StringComparison.OrdinalIgnoreCase)),
                        _ => true
                    };
                });

                ShowToast($"🔍 '{clicked.DisplayName}' Filter Applied");
            }
        }

        private void FolderItemPanel_Drop(object sender, DragEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not FolderNode targetFolder) return;

            if (e.Data.GetDataPresent("ReFanS_FolderOrder") && e.Data.GetData("ReFanS_FolderOrder") is FolderNode sourceFolder && sourceFolder != targetFolder)
            {
                if (FolderList.Contains(sourceFolder) && FolderList.Contains(targetFolder))
                {
                    FolderList.Move(FolderList.IndexOf(sourceFolder), FolderList.IndexOf(targetFolder));
                    SaveFolders();
                }
                e.Handled = true;
                return;
            }

            if (e.Data.GetDataPresent("ReFanS_CardItem") && e.Data.GetData("ReFanS_CardItem") is ReferenceItem droppedItem)
            {
                var selectedItems = GalleryList.SelectedItems.Cast<ReferenceItem>().ToList();
                if (!selectedItems.Contains(droppedItem)) selectedItems = new List<ReferenceItem> { droppedItem };

                foreach (var item in selectedItems)
                {
                    item.FolderName = targetFolder.Name;
                    item.IsDeleted = false;
                }
                SaveAllData();
                ShowToast($"📁 {selectedItems.Count} items moved to '{targetFolder.Name}' folder!");
                e.Handled = true;
            }
        }

        // --- Custom Filter & Tag Manager ---
        private void FilterTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (item != null) { item.IsSelected = true; item.Focus(); }
        }

        private void LoadCustomTagsCatalog()
        {
            if (File.Exists(_customMetaFilePath))
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(_customMetaFilePath));
                    if (doc.RootElement.TryGetProperty("MasterTags", out var tagsElem))
                    {
                        var tags = JsonSerializer.Deserialize<ObservableCollection<LabelTag>>(tagsElem.GetRawText());
                        if (tags != null) MasterTagCatalog = tags;
                    }
                    if (doc.RootElement.TryGetProperty("CustomGroups", out var grpElem))
                    {
                        var grps = JsonSerializer.Deserialize<List<string>>(grpElem.GetRawText());
                        if (grps != null) CustomFilterGroups = grps;
                    }
                    ManagerTagListBox.ItemsSource = MasterTagCatalog;
                }
                catch { }
            }
        }

        private void SaveCustomTagsCatalog()
        {
            try
            {
                var data = new { MasterTags = MasterTagCatalog, CustomGroups = CustomFilterGroups };
                var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                File.WriteAllText(_customMetaFilePath, JsonSerializer.Serialize(data, options));
            }
            catch { }
        }

        private void AddCustomFilterGroup_Click(object sender, RoutedEventArgs e)
        {
            string? groupName = PromptInput(
                Loc("追加するカスタムフィルターグループ名を入力してください:", "추가할 커스텀 필터 그룹 이름을 입력하세요:", "Enter custom filter group name:"),
                Loc("カスタムグループ追加", "커스텀 필터 그룹 추가", "Add Custom Filter Group"),
                Loc("ムード", "분위기", "Mood"));
            if (!string.IsNullOrWhiteSpace(groupName))
            {
                string clean = groupName.Trim();
                if (!CustomFilterGroups.Contains(clean)) CustomFilterGroups.Add(clean);
                string defaultSubTag = $"{clean}/{Loc("基本タグ", "기본태그", "Default")}";
                if (!MasterTagCatalog.Any(t => t.Name.Equals(defaultSubTag, StringComparison.OrdinalIgnoreCase)))
                    MasterTagCatalog.Add(new LabelTag { Name = defaultSubTag, HexColor = "#61AFEF" });

                SaveCustomTagsCatalog();
                RefreshTagCloud();
            }
        }

        private void AddTagFromTree_Click(object sender, RoutedEventArgs e)
        {
            string prefix = "";
            if (FilterTreeView.SelectedItem is FilterTreeNode node && !node.IsCategoryHeader)
                prefix = node.FullMatchKey + "/";
            else if (FilterTreeView.SelectedItem is FilterTreeNode headerNode && headerNode.DisplayName.StartsWith("📌 "))
                prefix = headerNode.DisplayName.Replace("📌 ", "").Trim() + "/";

            string? tagName = PromptInput(
                Loc("新しいタグ (または 親タグ/サブタグ) の名前を入力してください:", "새 태그(또는 하위 태그) 이름을 입력하세요:", "Enter new tag (or Parent/Subtag) name:"),
                Loc("タグ追加", "태그 추가", "Add Tag"),
                prefix + Loc("新規タグ", "새태그", "NewTag"));
            if (!string.IsNullOrWhiteSpace(tagName))
            {
                MasterTagCatalog.Add(new LabelTag { Name = tagName.Trim(), HexColor = "#98C379" });
                SaveCustomTagsCatalog();
                RefreshTagCloud();
            }
        }

        private void RenameTagFromTree_Click(object sender, RoutedEventArgs e)
        {
            if (FilterTreeView.SelectedItem is FilterTreeNode node && !node.IsCategoryHeader)
            {
                string? newName = PromptInput(
                    Loc("変更するタグ名を入力してください (全アイテムに一括反映されます):", "변경할 이름을 입력하세요 (모든 항목에 일괄 반영됩니다):", "Enter new tag name (applied globally):"),
                    Loc("タグ名変更", "이름 변경", "Rename Tag"),
                    node.FullMatchKey);
                if (!string.IsNullOrWhiteSpace(newName))
                    RenameTagGlobally(node.FullMatchKey, newName.Trim());
            }
        }

        private void DeleteTagFromTree_Click(object sender, RoutedEventArgs e)
        {
            if (FilterTreeView.SelectedItem is FilterTreeNode node)
            {
                if (node.IsCategoryHeader && node.DisplayName.StartsWith("📌 "))
                {
                    string grpName = node.DisplayName.Replace("📌 ", "").Trim();
                    CustomFilterGroups.Remove(grpName);
                    SaveCustomTagsCatalog();
                    RefreshTagCloud();
                    return;
                }
                if (!node.IsCategoryHeader && MessageBox.Show(
                    Loc($"'{node.DisplayName}' タグを削除しますか？", $"'{node.DisplayName}' 태그를 삭제할까요?", $"Delete tag '{node.DisplayName}'?"),
                    Loc("タグ削除", "태그 삭제", "Delete Tag"), MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    DeleteTagGlobally(node.FullMatchKey);
                }
            }
        }

        private void OpenTagManager_Click(object sender, RoutedEventArgs e)
        {
            RefreshTagCloud();
            TagManagerModalOverlay.Visibility = Visibility.Visible;
            TagManagerModalOverlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        }

        private void CloseTagManager_Click(object sender, RoutedEventArgs e)
        {
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, args) => TagManagerModalOverlay.Visibility = Visibility.Collapsed;
            TagManagerModalOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void TagManagerOverlayBg_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == TagManagerModalOverlay) CloseTagManager_Click(sender, e);
        }

        // --- Right Tag Manager Logic ---
        private void ManagerAddMasterTag_Click(object sender, RoutedEventArgs e)
        {
            string name = ManagerNewTagNameBox.Text.Trim();
            string hex = ManagerColorPickerBtn.Content?.ToString() ?? "#7FB3D5";
            if (string.IsNullOrEmpty(name)) return;

            if (!MasterTagCatalog.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                MasterTagCatalog.Add(new LabelTag { Name = name, HexColor = hex });

            ManagerNewTagNameBox.Clear();
            SaveCustomTagsCatalog();
            RefreshTagCloud();
            ShowToast($"🏷️ '{name}' Tag Added ");
        }

        private void ManagerRenameTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is LabelTag tag)
            {
                string? newName = PromptInput(
                    Loc("新しいタグ名を入力してください:", "새 태그 이름을 입력하세요:", "Enter new tag name:"),
                    Loc("タグ名変更", "태그 이름 변경", "Rename Tag"),
                    tag.Name);
                if (!string.IsNullOrWhiteSpace(newName))
                    RenameTagGlobally(tag.Name, newName.Trim());
            }
        }

        private void ManagerRecolorTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is LabelTag tag)
            {
                OpenUniversalColorPicker(fe, hex =>
                {
                    tag.HexColor = hex;
                    foreach (var item in ReferenceList)
                        foreach (var l in item.Labels.Where(x => x.Name.Equals(tag.Name, StringComparison.OrdinalIgnoreCase)))
                            l.HexColor = hex;
                    SaveCustomTagsCatalog();
                    SaveAllData();
                    ManagerTagListBox.Items.Refresh();
                    ShowToast($"🎨 '{tag.Name}' Color changed ({hex})");
                });
            }
        }

        private void ManagerDeleteTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is LabelTag tag)
            {
                if (MessageBox.Show(
                    Loc($"'{tag.Name}' タグを完全に削除しますか？", $"'{tag.Name}' 태그를 완전히 삭제할까요?", $"Permanently delete tag '{tag.Name}'?"),
                    Loc("タグ削除", "태그 삭제", "Delete Tag"), MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    DeleteTagGlobally(tag.Name);
                }
            }
        }

        private void RenameTagGlobally(string oldName, string newName)
        {
            foreach (var m in MasterTagCatalog.Where(t => t.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase)))
                m.Name = newName;
            foreach (var item in ReferenceList)
                foreach (var l in item.Labels.Where(t => t.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase)))
                    l.Name = newName;

            SaveCustomTagsCatalog();
            SaveAllData();
            GalleryList.Items.Refresh();
            ShowToast($"✏️ '{oldName}' -> '{newName}' Change Complete ");
        }

        private void UpdateTagColorGlobally(string tagName, string newHexColor)
        {
            foreach (var mTag in MasterTagCatalog)
            {
                if (mTag.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase))
                    mTag.HexColor = newHexColor;
            }

            foreach (var item in ReferenceList)
            {
                foreach (var label in item.Labels)
                {
                    if (label.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase))
                        label.HexColor = newHexColor;
                }
            }

            SaveCustomTagsCatalog();
            SaveAllData();
            RefreshTagCloud();
        }

        private void DeleteTagGlobally(string targetName)
        {
            var toRemoveMaster = MasterTagCatalog.Where(t => t.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase) || t.Name.StartsWith(targetName + "/", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var m in toRemoveMaster) MasterTagCatalog.Remove(m);

            foreach (var item in ReferenceList)
            {
                var toRemove = item.Labels.Where(t => t.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase) || t.Name.StartsWith(targetName + "/", StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var r in toRemove) item.Labels.Remove(r);
            }

            SaveCustomTagsCatalog();
            SaveAllData();
            GalleryList.Items.Refresh();
            ShowToast($"🗑️ '{targetName}' Tag Deleted ");
        }

        // --- Capture Screen  ---
        private async void CaptureScreen_Click(object sender, RoutedEventArgs e)
        {
            var prevState = this.WindowState == WindowState.Minimized ? WindowState.Normal : this.WindowState;
            this.WindowState = WindowState.Minimized;
            await Task.Delay(320);

            try
            {
                int w = (int)SystemParameters.PrimaryScreenWidth;
                int h = (int)SystemParameters.PrimaryScreenHeight;
                SaveCapturedScreenRect(new Int32Rect(0, 0, w, h), "Capture");
            }
            catch { ShowToast("⚠️ Failed Capture"); }
            finally
            {
                this.WindowState = prevState;
                this.Activate();
            }
        }

        private async void CaptureRegionScreen_Click(object sender, RoutedEventArgs e)
        {
            var prevState = this.WindowState == WindowState.Minimized ? WindowState.Normal : this.WindowState;
            this.WindowState = WindowState.Minimized;
            await Task.Delay(320);

            try
            {
                Window snipWin = new Window
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)),
                    Topmost = true,
                    Cursor = Cursors.Cross,
                    Left = 0,
                    Top = 0,
                    Width = SystemParameters.PrimaryScreenWidth,
                    Height = SystemParameters.PrimaryScreenHeight
                };

                Canvas canvas = new Canvas();
                Border cropBox = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(0, 170, 255)), BorderThickness = new Thickness(2), 
                    Background = new SolidColorBrush(Color.FromArgb(40, 0, 170, 255)), Visibility = Visibility.Collapsed };
                canvas.Children.Add(cropBox);
                snipWin.Content = canvas;

                Point startPt = new Point();
                bool isDragging = false;
                Int32Rect selectedRect = Int32Rect.Empty;

                snipWin.MouseLeftButtonDown += (s, ev) =>
                {
                    isDragging = true;
                    startPt = ev.GetPosition(canvas);
                    Canvas.SetLeft(cropBox, startPt.X);
                    Canvas.SetTop(cropBox, startPt.Y);
                    cropBox.Width = 0; cropBox.Height = 0;
                    cropBox.Visibility = Visibility.Visible;
                };

                snipWin.MouseMove += (s, ev) =>
                {
                    if (!isDragging) return;
                    Point cur = ev.GetPosition(canvas);
                    Canvas.SetLeft(cropBox, Math.Min(startPt.X, cur.X));
                    Canvas.SetTop(cropBox, Math.Min(startPt.Y, cur.Y));
                    cropBox.Width = Math.Abs(cur.X - startPt.X);
                    cropBox.Height = Math.Abs(cur.Y - startPt.Y);
                };

                snipWin.MouseLeftButtonUp += (s, ev) =>
                {
                    isDragging = false;
                    selectedRect = new Int32Rect((int)Canvas.GetLeft(cropBox), (int)Canvas.GetTop(cropBox), (int)cropBox.Width, (int)cropBox.Height);
                    snipWin.DialogResult = true;
                    snipWin.Close();
                };

                snipWin.KeyDown += (s, ev) => { if (ev.Key == Key.Escape) { snipWin.DialogResult = false; snipWin.Close(); } };

                if (snipWin.ShowDialog() == true && selectedRect.Width > 10 && selectedRect.Height > 10)
                {
                    await Task.Delay(100);
                    SaveCapturedScreenRect(selectedRect, "Capture");
                }
            }
            catch { }
            finally
            {
                this.WindowState = prevState;
                this.Activate();
            }
        }

        private void SaveCapturedScreenRect(Int32Rect rect, string prefix)
        {
            using var bmp = new System.Drawing.Bitmap(rect.Width, rect.Height);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(rect.X, rect.Y, 0, 0, new System.Drawing.Size(rect.Width, rect.Height));
            }

            string fileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string destPath = Path.Combine(_imageFolderDir, fileName);
            bmp.Save(destPath, System.Drawing.Imaging.ImageFormat.Png);

            var newItem = new ReferenceItem
            {
                ItemType = "Image",
                Title = $"{prefix} ({DateTime.Now:HH:mm:ss})",
                Creator = "Screenshot",
                CoverImagePath = destPath,
                MediaFilePath = destPath,
                FolderName = UserFolderTree.SelectedItem is FolderNode fn ? fn.Name : "Default"
            };
            AnalyzeImageFeatures(newItem);
            ReferenceList.Insert(0, newItem);
            GalleryList.SelectedItem = newItem;
            SaveAllData();
            ShowToast(Loc("📸 キャプチャを登録しました！", "📸 캡처 등록 완료!", "📸 Screen capture saved!"));
        }

        // --- Change Thumbnail to Clipboard Image ---
        private void PasteClipboardThumbnail_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item && Clipboard.ContainsImage())
            {
                try
                {
                    BitmapSource clipImg = Clipboard.GetImage();
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(clipImg));

                    string destPath = Path.Combine(_imageFolderDir, $"clip_{Guid.NewGuid().ToString().Substring(0, 8)}.png");
                    using (FileStream fs = new FileStream(destPath, FileMode.Create)) encoder.Save(fs);

                    item.CoverImagePath = destPath;
                    AnalyzeImageFeatures(item);
                    SaveAllData();
                    GalleryList.Items.Refresh();
                    ShowToast("✨ Changed Thumbnail to Clipboard Image");
                }
                catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
            }
            else
            {
                ShowToast("⚠️ There is no image copied to the clipboard.");
            }
        }

        // --- Direct Capture   ---
        private async void CaptureImmediateScreen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int w = (int)SystemParameters.PrimaryScreenWidth;
                int h = (int)SystemParameters.PrimaryScreenHeight;

                using var bmp = new System.Drawing.Bitmap(w, h);
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                }

                string destPath = Path.Combine(_imageFolderDir, $"capture_full_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                bmp.Save(destPath, System.Drawing.Imaging.ImageFormat.Png);

                var newItem = new ReferenceItem
                {
                    ItemType = "Image",
                    Title = $"Capture ({DateTime.Now:HH:mm:ss})",
                    Creator = "Screen Capture",
                    CoverImagePath = destPath,
                    MediaFilePath = destPath,
                    FolderName = UserFolderTree.SelectedItem is FolderNode fn ? fn.Name : "Default"
                };
                AnalyzeImageFeatures(newItem);
                ReferenceList.Insert(0, newItem);
                GalleryList.SelectedItem = newItem;
                SaveAllData();
                ShowToast(Loc("📸 キャプチャを登録しました！", "📸 캡처 등록 완료!", "📸 Screen capture saved!"));
            }
            catch { ShowToast("⚠️ Capture Failed"); }
        }

        // --- Inline Mini Media Player ---
        private void CardMiniPlay_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ReferenceItem item && item.IsPlayableMedia)
            {
                bool wasPlaying = item.IsInlinePlaying;
                StopAllActiveMedia(); 

                if (!wasPlaying)
                {
                    item.IsInlinePlaying = true;

                    var cardContainer = FindAncestor<ListBoxItem>(btn);
                    var cardMediaElement = FindVisualChildren<MediaElement>(cardContainer).FirstOrDefault();

                    if (cardMediaElement != null)
                    {
                        _activeInlineMediaElement = cardMediaElement;
                        cardMediaElement.UpdateLayout();
                        cardMediaElement.Source = new Uri(item.MediaFilePath, UriKind.Absolute);
                        cardMediaElement.Position = TimeSpan.Zero;
                        cardMediaElement.Play();
                    }

                    if (_inlinePlayTimer == null)
                    {
                        _inlinePlayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                        _inlinePlayTimer.Tick += (s, ev) =>
                        {
                            if (_activeInlineMediaElement != null &&
                                _activeInlineMediaElement.DataContext is ReferenceItem activeItem &&
                                activeItem.IsInlinePlaying &&
                                _activeInlineMediaElement.NaturalDuration.HasTimeSpan)
                            {
                                double curSec = _activeInlineMediaElement.Position.TotalSeconds;
                                double totSec = _activeInlineMediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                                activeItem.InlineProgress = totSec > 0 ? (curSec / totSec) * 100.0 : 0;
                                activeItem.InlineTimeText = $"{_activeInlineMediaElement.Position:mm\\:ss} / {_activeInlineMediaElement.NaturalDuration.TimeSpan:mm\\:ss}";
                            }
                        };
                        _inlinePlayTimer.Start();
                    }
                }
                e.Handled = true;
            }
        }

        private void CardInlineProgressBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ProgressBar pb && pb.DataContext is ReferenceItem item && item.IsInlinePlaying && _activeInlineMediaElement != null)
            {
                if (pb.ActualWidth > 0 && _activeInlineMediaElement.NaturalDuration.HasTimeSpan)
                {
                    double ratio = Math.Clamp(e.GetPosition(pb).X / pb.ActualWidth, 0, 1);
                    double targetSec = ratio * _activeInlineMediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                    _activeInlineMediaElement.Position = TimeSpan.FromSeconds(targetSec);
                    item.InlineProgress = ratio * 100.0;
                }
                e.Handled = true;
            }
        }

        private void CloseMiniPreview_Click(object sender, RoutedEventArgs e)
        {
            MiniPreviewMediaElement.Stop();
            MiniPreviewMediaElement.Source = null;
            SharedMediaPlayer.Stop();
            MiniPreviewPlayerBar.Visibility = Visibility.Collapsed;
        }

        private void MiniPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (MiniPreviewMediaElement.Visibility == Visibility.Visible)
            {
                if (MiniPlayPauseBtn.Content.ToString() == "⏸") { MiniPreviewMediaElement.Pause(); MiniPlayPauseBtn.Content = "▶"; }
                else { MiniPreviewMediaElement.Play(); MiniPlayPauseBtn.Content = "⏸"; }
            }
            else
            {
                if (MiniPlayPauseBtn.Content.ToString() == "⏸") { SharedMediaPlayer.Pause(); MiniPlayPauseBtn.Content = "▶"; }
                else { SharedMediaPlayer.Play(); MiniPlayPauseBtn.Content = "⏸"; }
            }
        }

        private void MiniPreviewMedia_Opened(object sender, RoutedEventArgs e)
        {
            if (MiniPreviewMediaElement.NaturalDuration.HasTimeSpan)
                MiniPreviewSeekSlider.Maximum = MiniPreviewMediaElement.NaturalDuration.TimeSpan.TotalSeconds;
        }

        private void MiniPreviewMedia_Ended(object sender, RoutedEventArgs e)
        {
            MiniPreviewMediaElement.Position = TimeSpan.Zero;
            MiniPreviewMediaElement.Play();
        }

        private void MiniPreviewSeek_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (MiniPreviewSeekSlider.ActualWidth > 0 && MiniPreviewMediaElement.NaturalDuration.HasTimeSpan)
            {
                double ratio = Math.Clamp(e.GetPosition(MiniPreviewSeekSlider).X / MiniPreviewSeekSlider.ActualWidth, 0, 1);
                double targetSec = ratio * MiniPreviewMediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                MiniPreviewSeekSlider.Value = targetSec;
                MiniPreviewMediaElement.Position = TimeSpan.FromSeconds(targetSec);
            }
        }

        // --- Custom Theme & Wallpaper ---
        private void PickCustomThemeTone_Click(object sender, RoutedEventArgs e)
        {
            OpenUniversalColorPicker(BtnCustomThemeColor, hex =>
            {
                try
                {
                    Color baseCol = (Color)ColorConverter.ConvertFromString(hex);
                    this.Resources["ThemeCenterBg"] = new SolidColorBrush(baseCol);
                    this.Resources["ThemeSidebarBg"] = new SolidColorBrush(Color.FromArgb(200, (byte)(baseCol.R * 0.85), (byte)(baseCol.G * 0.85), (byte)(baseCol.B * 0.85)));
                    this.Resources["ThemeCardBg"] = new SolidColorBrush(Color.FromRgb((byte)Math.Min(255, baseCol.R * 1.15), (byte)Math.Min(255, baseCol.G * 1.15), (byte)Math.Min(255, baseCol.B * 1.15)));
                    ShowToast(Loc($"🎨 カスタムテーマ色を適用しました ({hex})", $"🎨 커스텀 테마 색상 적용 완료 ({hex})", $"🎨 Custom theme color applied ({hex})"));
                }
                catch { }
            });
        }

        private void SelectCustomWallpaper_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Title = Loc("壁紙画像を選択", "배경 사진 선택", "Select Wallpaper Image"), Filter = "Image Files|*.jpg;*.jpeg;*.png;*.webp" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string destPath = Path.Combine(_imageFolderDir, $"wallpaper_{Path.GetFileName(dlg.FileName)}");
                    File.Copy(dlg.FileName, destPath, true);

                    BitmapImage bmp = new BitmapImage(new Uri(destPath, UriKind.Absolute));
                    CustomBgWallpaperImage.Source = bmp;

                    // 배경 사진이 켜질 때 좌우측 사이드바 배경을 은은하게 반투명하게 만들어 사진이 전체에 걸치도록 연동!
                    this.Resources["ThemeSidebarBg"] = new SolidColorBrush(Color.FromArgb(180, 40, 40, 45));
                    ShowToast(Loc("🖼️ 背景画像が適用されました", "🖼️ 커스텀 배경 사진이 적용되었습니다!", "🖼️ Custom wallpaper applied!"));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void ClearCustomWallpaper_Click(object sender, RoutedEventArgs e)
        {
            CustomBgWallpaperImage.Source = null;
            ApplyThemeInternal(_currentTheme, showNotification: false); // 테마 원래 배경색으로 복구
            ShowToast(Loc("🗑️ 背景画像をリセットしました", "🗑️ 배경 사진이 초기화되었습니다.", "🗑️ Wallpaper reset."));
        }

        private void WallpaperOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CustomBgWallpaperImage != null)
                CustomBgWallpaperImage.Opacity = WallpaperOpacitySlider.Value;
        }

        // --- Ctrl + Z Logic ---
        private readonly Stack<LabelTag> _removedTagHistory = new Stack<LabelTag>();
        private void UndoRemovedTag_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item && _removedTagHistory.Count > 0)
            {
                var lastRemoved = _removedTagHistory.Pop();
                if (!item.Labels.Any(l => l.Name.Equals(lastRemoved.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    item.Labels.Add(lastRemoved);
                    SaveAllData();
                    ShowToast($"↩️ Tag '{lastRemoved.Name}' Restored");
                }
            }
            else
            {
                ShowToast("⚠️ There are no recently deleted tags to restore.");
            }
        }

        // Right Side Tag Manager
        private void OpenItemTagManager_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is not ReferenceItem item) return;
            if (item.IsLocked)
            {
                ShowToast(Loc("🔒 ロックされたファイルです。", "🔒 잠긴 파일입니다.", "🔒 File is locked."));
                return;
            }

            Window tagWin = new Window
            {
                Width = 540,
                Height = 580,
                Title = Loc($"タグ管理 — {item.Title}", $"파일 태그 관리 — {item.Title}", $"Tag Manager — {item.Title}"),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 34)),
                Foreground = Brushes.White,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };
            tagWin.PreviewKeyDown += (s, ev) => { if (ev.Key == Key.Escape) tagWin.Close(); };

            Grid root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 1. Upper Title
            TextBlock titleBlock = new TextBlock
            {
                Text = Loc($"🏷️ '{item.Title}' タグ & カラー管理", $"🏷️ '{item.Title}' 파일 태그 & 컬러 관리자", $"🏷️ Manage Tags for '{item.Title}'"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 14)
            };
            Grid.SetRow(titleBlock, 0);
            root.Children.Add(titleBlock);

            // 2. New Tag + Color Change
            Border addBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 22, 26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 62)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid addGrid = new Grid();
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBox newTagBox = new TextBox { Background = new SolidColorBrush(Color.FromRgb(37, 37, 42)), Foreground = Brushes.White, 
                BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6), FontSize = 12.5 };
            string pickedHex = "#61AFEF";
            Button colorPickBtn = new Button { Content = "🎨 #61AFEF", Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(pickedHex)), 
                Foreground = new SolidColorBrush(Color.FromRgb(20, 20, 20)), FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0), 
                Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(6, 0, 6, 0), Cursor = Cursors.Hand };
            colorPickBtn.Click += (s, ev) =>
            {
                OpenUniversalColorPicker(colorPickBtn, hex =>
                {
                    pickedHex = hex;
                    colorPickBtn.Content = $"🎨 {hex}";
                    colorPickBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                });
            };

            Button addTagBtn = new Button { Content = Loc("+ タグ追加", "+ 태그 추가", "+ Add Tag"), Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)), 
                Foreground = Brushes.White, FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0), Padding = new Thickness(14, 6, 14, 6), Cursor = Cursors.Hand };
            Grid.SetColumn(newTagBox, 0); Grid.SetColumn(colorPickBtn, 1); Grid.SetColumn(addTagBtn, 2);
            addGrid.Children.Add(newTagBox); addGrid.Children.Add(colorPickBtn); addGrid.Children.Add(addTagBtn);
            addBar.Child = addGrid;
            Grid.SetRow(addBar, 1);
            root.Children.Add(addBar);

            // 3. Tag List Area
            Border listBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(20, 20, 24)), CornerRadius = new CornerRadius(8), 
                Padding = new Thickness(10), BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 56)), BorderThickness = new Thickness(1) };
            ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel listStack = new StackPanel();
            sv.Content = listStack;
            listBorder.Child = sv;
            Grid.SetRow(listBorder, 2);
            root.Children.Add(listBorder);

            void RebuildTagRows()
            {
                listStack.Children.Clear();

                foreach (var l in item.Labels)
                {
                    if (!MasterTagCatalog.Any(m => m.Name.Equals(l.Name, StringComparison.OrdinalIgnoreCase)))
                        MasterTagCatalog.Add(new LabelTag { Name = l.Name, HexColor = l.HexColor });
                }

                foreach (var mTag in MasterTagCatalog.ToList())
                {
                    Border rowBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(32, 32, 38)), CornerRadius = new CornerRadius(6), 
                        Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 0, 0, 6) };
                    Grid rowGrid = new Grid();
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    bool isAssigned = item.Labels.Any(l => l.Name.Equals(mTag.Name, StringComparison.OrdinalIgnoreCase));
                    CheckBox chk = new CheckBox { IsChecked = isAssigned, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), Cursor = Cursors.Hand };
                    chk.Checked += (s, ev) =>
                    {
                        if (!item.Labels.Any(l => l.Name.Equals(mTag.Name, StringComparison.OrdinalIgnoreCase)))
                            item.Labels.Add(new LabelTag { Name = mTag.Name, HexColor = mTag.HexColor });
                        SaveAllData();
                    };
                    chk.Unchecked += (s, ev) =>
                    {
                        var target = item.Labels.FirstOrDefault(l => l.Name.Equals(mTag.Name, StringComparison.OrdinalIgnoreCase));
                        if (target != null) { _removedTagHistory.Push(target); item.Labels.Remove(target); SaveAllData(); }
                    };

                    Border colorChip = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(mTag.HexColor)), Margin = new Thickness(0, 0, 10, 0), BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), BorderThickness = new Thickness(1) };
                    TextBlock nameTxt = new TextBlock { Text = mTag.Name, Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };

                    Button btnRename = new Button { Content = Loc("✏️ 名前変更", "✏️ 이름변경", "✏️ Rename"), Background = new SolidColorBrush(Color.FromRgb(50, 50, 58)), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontSize = 11, Padding = new Thickness(7, 3, 7, 3), Margin = new Thickness(3, 0, 3, 0), Cursor = Cursors.Hand };
                    btnRename.Click += (s, ev) =>
                    {
                        string? nn = PromptInput(Loc("新しいタグ名を入力してください:", "새 태그 이름을 입력하세요:", "Enter new tag name:"), Loc("タグ名変更", "태그 이름 변경", "Rename Tag"), mTag.Name);
                        if (!string.IsNullOrWhiteSpace(nn)) { RenameTagGlobally(mTag.Name, nn.Trim()); RebuildTagRows(); }
                    };

                    Button btnColor = new Button { Content = Loc("🎨 色変更", "🎨 색상변경", "🎨 Color"), Background = new SolidColorBrush(Color.FromRgb(50, 50, 58)), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontSize = 11, Padding = new Thickness(7, 3, 7, 3), Margin = new Thickness(3, 0, 3, 0), Cursor = Cursors.Hand };
                    btnColor.Click += (s, ev) =>
                    {
                        OpenUniversalColorPicker(btnColor, hex => { UpdateTagColorGlobally(mTag.Name, hex); RebuildTagRows(); });
                    };

                    Button btnDel = new Button { Content = "🗑️", Background = new SolidColorBrush(Color.FromRgb(65, 35, 35)), Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107)), BorderThickness = new Thickness(0), FontSize = 11, Padding = new Thickness(7, 3, 7, 3), Margin = new Thickness(3, 0, 0, 0), Cursor = Cursors.Hand };
                    btnDel.Click += (s, ev) =>
                    {
                        var target = item.Labels.FirstOrDefault(l => l.Name.Equals(mTag.Name, StringComparison.OrdinalIgnoreCase));
                        if (target != null) { _removedTagHistory.Push(target); item.Labels.Remove(target); SaveAllData(); RebuildTagRows(); }
                    };

                    Grid.SetColumn(chk, 0); Grid.SetColumn(colorChip, 1); Grid.SetColumn(nameTxt, 2);
                    Grid.SetColumn(btnRename, 3); Grid.SetColumn(btnColor, 4); Grid.SetColumn(btnDel, 5);
                    rowGrid.Children.Add(chk); rowGrid.Children.Add(colorChip); rowGrid.Children.Add(nameTxt);
                    rowGrid.Children.Add(btnRename); rowGrid.Children.Add(btnColor); rowGrid.Children.Add(btnDel);
                    rowBorder.Child = rowGrid;
                    listStack.Children.Add(rowBorder);
                }
            }

            addTagBtn.Click += (s, ev) =>
            {
                string raw = newTagBox.Text.Trim();
                if (!string.IsNullOrEmpty(raw))
                {
                    if (!MasterTagCatalog.Any(m => m.Name.Equals(raw, StringComparison.OrdinalIgnoreCase)))
                        MasterTagCatalog.Add(new LabelTag { Name = raw, HexColor = pickedHex });
                    if (!item.Labels.Any(l => l.Name.Equals(raw, StringComparison.OrdinalIgnoreCase)))
                        item.Labels.Add(new LabelTag { Name = raw, HexColor = pickedHex });
                    newTagBox.Clear();
                    SaveCustomTagsCatalog();
                    SaveAllData();
                    RefreshTagCloud();
                    RebuildTagRows();
                }
            };

            RebuildTagRows();

            // 4. Close Button
            Button doneBtn = new Button
            {
                Content = Loc("完了して閉じる (ESC)", "완료 및 닫기 (ESC)", "Done & Close (ESC)"),
                Height = 36,
                Margin = new Thickness(0, 14, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(24, 0, 24, 0),
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            doneBtn.Click += (s, ev) => { tagWin.Close(); SaveAllData(); RefreshTagCloud(); };
            Grid.SetRow(doneBtn, 3);
            root.Children.Add(doneBtn);

            tagWin.Content = root;
            tagWin.ShowDialog();
        }

        // --- .md .txt Renderer ---
        private void RenderMarkdownOrTextToViewer(string title, string rawText)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Pretendard, Inter, Segoe UI, Malgun Gothic"),
                Foreground = (Brush)this.Resources["ThemeTextPrimary"],
                PagePadding = new Thickness(10)
            };

            doc.Blocks.Add(new Paragraph(new Run(title))
            {
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(86, 156, 214)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            if (string.IsNullOrEmpty(rawText))
            {
                doc.Blocks.Add(new Paragraph(new Run("(Empty, press Space to edit)")));
                MarkdownDocViewer.Document = doc;
                return;
            }

            foreach (string rawLine in rawText.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                var para = new Paragraph { Margin = new Thickness(0, 0, 0, 8), LineHeight = 22 };

                if (line.StartsWith("# "))
                    para.Inlines.Add(new Run(line.Substring(2)) { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(156, 220, 254)) });
                else if (line.StartsWith("## "))
                    para.Inlines.Add(new Run(line.Substring(3)) { FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(186, 255, 201)) });
                else if (line.StartsWith("### "))
                    para.Inlines.Add(new Run(line.Substring(4)) { FontSize = 15.5, FontWeight = FontWeights.Bold });
                else if (line.StartsWith("- ") || line.StartsWith("* "))
                {
                    para.Margin = new Thickness(14, 0, 0, 6);
                    para.Inlines.Add(new Run("• " + line.Substring(2)) { FontSize = 14.5 });
                }
                else if (line.StartsWith("> "))
                {
                    para.Margin = new Thickness(12, 4, 0, 8);
                    para.Padding = new Thickness(10, 4, 4, 4);
                    para.BorderBrush = new SolidColorBrush(Color.FromRgb(86, 156, 214));
                    para.BorderThickness = new Thickness(3, 0, 0, 0);
                    para.Inlines.Add(new Run(line.Substring(2)) { FontStyle = FontStyles.Italic, Foreground = Brushes.LightGray });
                }
                else
                    para.Inlines.Add(new Run(line) { FontSize = 14.5 });

                doc.Blocks.Add(para);
            }
            MarkdownDocViewer.Document = doc;
        }

        // --- Scan Fonts & Video Thumbnail Capture ---
        private List<string> ExtractFontsFromDocument(string filePath)
        {
            var foundFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return foundFonts.ToList();

            try
            {
                using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                int maxRead = (int)Math.Min(fs.Length, 8 * 1024 * 1024);
                byte[] buffer = new byte[maxRead];
                int bytesRead = fs.ReadAtLeast(buffer, maxRead, throwOnEndOfStream: false);
                string rawContent = Encoding.Latin1.GetString(buffer, 0, bytesRead);

                foreach (Match m in Regex.Matches(rawContent, @"/BaseFont\s*/([A-Za-z0-9\+\-_]+)"))
                {
                    string fontName = m.Groups[1].Value;
                    if (fontName.Length > 7 && fontName[6] == '+') fontName = fontName.Substring(7);
                    if (fontName.Length >= 3) foundFonts.Add(fontName);
                }
                foreach (Match m in Regex.Matches(rawContent, @"font-family\s*[:=]\s*[""']?([A-Za-z0-9\s\-_]+)[""']?", RegexOptions.IgnoreCase))
                {
                    string fontName = m.Groups[1].Value.Trim();
                    if (fontName.Length >= 3 && !fontName.Equals("sans-serif", StringComparison.OrdinalIgnoreCase)) foundFonts.Add(fontName);
                }
                foreach (Match m in Regex.Matches(rawContent, @"(?:FontName|/FontName)\s*\(?([A-Za-z0-9\-_]{3,35})\)?"))
                    foundFonts.Add(m.Groups[1].Value.Trim());
                foreach (Match m in Regex.Matches(rawContent, @"\b([A-Z][a-zA-Z0-9]+(?:-(?:Bold|Regular|Medium|Light|Black|Italic|SemiBold|ExtraBold)))\b"))
                {
                    string candidate = m.Groups[1].Value;
                    if (!candidate.StartsWith("Adobe") && !candidate.StartsWith("Mac") && !candidate.StartsWith("Win")) foundFonts.Add(candidate);
                }
            }
            catch { }
            return foundFonts.Take(20).ToList();
        }

        private void RescanDocumentFonts_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item && !string.IsNullOrEmpty(item.MediaFilePath))
            {
                var fonts = ExtractFontsFromDocument(item.MediaFilePath);
                item.DetectedFonts.Clear();
                foreach (var f in fonts) item.DetectedFonts.Add(f);
                if (item.DetectedFonts.Count == 0) item.DetectedFonts.Add("No Fonts Found");
                SaveAllData();
                ShowToast($"🔍 Scan Complete: {fonts.Count} fonts found!");
            }
        }

        private void DetectedFontChip_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is string fontName)
            {
                Clipboard.SetText(fontName);
                ShowToast($"🔤 Font name '{fontName}' copied!");
            }
        }

        private string GenerateFileTypePlaceholderCover(string ext, string categoryTitle, string subInfo, Color accentColor)
        {
            int w = 500, h = 500;
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(26, 26, 30)), null, new Rect(0, 0, w, h));
                dc.DrawRoundedRectangle(new SolidColorBrush(accentColor), null, new Rect(40, 40, 420, 420), 28, 28);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(34, 34, 38)), null, new Rect(46, 46, 408, 408), 24, 24);

                FormattedText extText = new FormattedText(ext.ToUpper(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal), 88, new SolidColorBrush(accentColor), 96);
                dc.DrawText(extText, new Point((w - extText.Width) / 2, 140));

                FormattedText catText = new FormattedText(categoryTitle, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Malgun Gothic, Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 28, Brushes.White, 96);
                dc.DrawText(catText, new Point((w - catText.Width) / 2, 275));

                FormattedText subText = new FormattedText(subInfo, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Consolas, Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 18, Brushes.Gray, 96);
                dc.DrawText(subText, new Point((w - subText.Width) / 2, 330));
            }
            RenderTargetBitmap bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            string destPath = Path.Combine(_imageFolderDir, $"badge_{Guid.NewGuid().ToString().Substring(0, 8)}.png");
            using (FileStream fs = new FileStream(destPath, FileMode.Create)) encoder.Save(fs);
            return destPath;
        }

        private Task ExtractVideoThumbnailAsync(ReferenceItem item, string videoFilePath)
        {
            var tcs = new TaskCompletionSource<bool>();
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    MediaPlayer player = new MediaPlayer { Volume = 0, ScrubbingEnabled = true };
                    player.Open(new Uri(videoFilePath, UriKind.Absolute));
                    player.MediaOpened += async (s, e) =>
                    {
                        try
                        {
                            double targetSec = player.NaturalDuration.HasTimeSpan ? Math.Min(1.0, player.NaturalDuration.TimeSpan.TotalSeconds * 0.2) : 0.5;
                            player.Position = TimeSpan.FromSeconds(targetSec);
                            await Task.Delay(350);

                            int vw = player.NaturalVideoWidth, vh = player.NaturalVideoHeight;
                            if (vw > 0 && vh > 0)
                            {
                                DrawingVisual dv = new DrawingVisual();
                                using (DrawingContext dc = dv.RenderOpen()) dc.DrawVideo(player, new Rect(0, 0, vw, vh));
                                RenderTargetBitmap bmp = new RenderTargetBitmap(vw, vh, 96, 96, PixelFormats.Pbgra32);
                                bmp.Render(dv);
                                PngBitmapEncoder encoder = new PngBitmapEncoder();
                                encoder.Frames.Add(BitmapFrame.Create(bmp));

                                string thumbPath = Path.Combine(_imageFolderDir, $"vthumb_{Guid.NewGuid().ToString().Substring(0, 8)}.png");
                                using (FileStream fs = new FileStream(thumbPath, FileMode.Create)) encoder.Save(fs);

                                item.CoverImagePath = thumbPath;
                                item.Resolution = $"{vw} x {vh} px (VIDEO)";
                                AnalyzeImageFeatures(item);
                                SaveAllData();
                                GalleryList.Items.Refresh();
                            }
                        }
                        catch { }
                        finally { player.Close(); tcs.TrySetResult(true); }
                    };
                    player.MediaFailed += (s, e) => { player.Close(); tcs.TrySetResult(false); };
                }
                catch { tcs.TrySetResult(false); }
            });
            return tcs.Task;
        }

        // --- Open External File & Reveal in Explorer ---
        private void OpenExternalFile_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item)
            {
                string target = !string.IsNullOrEmpty(item.MediaFilePath) && File.Exists(item.MediaFilePath) ? item.MediaFilePath : item.CoverImagePath;
                if (!string.IsNullOrEmpty(target) && File.Exists(target))
                {
                    try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
                    catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
                }
                else ShowToast("⚠️ No local source file connected.");
            }
        }

        private void RevealInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item)
            {
                string target = !string.IsNullOrEmpty(item.MediaFilePath) && File.Exists(item.MediaFilePath) ? item.MediaFilePath : item.CoverImagePath;
                if (!string.IsNullOrEmpty(target) && File.Exists(target))
                    Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"/select,\"{target}\"" });
            }
        }

        // --- Hover Preview for Magnifier ---
        private void Magnifier_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ReferenceItem item)
            {
                if (!string.IsNullOrEmpty(item.CoverImagePath) && File.Exists(item.CoverImagePath))
                {
                    HoverPreviewImage.Source = new BitmapImage(new Uri(item.CoverImagePath, UriKind.Absolute));
                    HoverPreviewTitle.Text = item.Title;
                    HoverPreviewSub.Text = $"{item.TypeBadgeText}  |  {item.Resolution}";
                    HoverPreviewOverlay.Visibility = Visibility.Visible;

                    var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                    HoverPreviewOverlay.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
                    HoverPreviewScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
                    HoverPreviewScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
                }
            }
        }

        private void Magnifier_MouseLeave(object sender, MouseEventArgs e)
        {
            var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(140));
            fadeOut.Completed += (s, args) => { if (HoverPreviewOverlay.Opacity < 0.05) HoverPreviewOverlay.Visibility = Visibility.Collapsed; };
            HoverPreviewOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            HoverPreviewScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, TimeSpan.FromMilliseconds(140)));
            HoverPreviewScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, TimeSpan.FromMilliseconds(140)));
        }

        // --- Audio & Media Controller ---
        private void AttachMediaFile_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item)
            {
                OpenFileDialog dlg = new OpenFileDialog { Title = "Select Media File", Filter = "All Reference Files|*.*" };
                if (dlg.ShowDialog() == true)
                {
                    item.MediaFilePath = CopyFileToMediaFolder(dlg.FileName);
                    LoadMediaForSelectedItem(item);
                    SaveAllData();
                    ShowToast("📂 File Connected Successfully");
                }
            }
        }

        private void LoadMediaForSelectedItem(ReferenceItem item)
        {
            StopMedia();
            WaveformImage.Source = GenerateWaveformBitmap(item.MediaFilePath, item.Title);

            string ext = item.FileExtension.ToLower();
            bool isPlayableMedia = ext == "mp3" || ext == "wav" || ext == "ogg" || ext == "flac" || ext == "mp4" || ext == "webm" || ext == "avi";

            if (isPlayableMedia && !string.IsNullOrEmpty(item.MediaFilePath) && File.Exists(item.MediaFilePath))
                SharedMediaPlayer.Source = new Uri(item.MediaFilePath, UriKind.Absolute);
            else
            {
                SharedMediaPlayer.Source = null;
                MediaTimeText.Text = "00:00 / 00:00";
            }
        }

        // --- Generate Waveform Bitmap for Audio Files ---
        private ImageSource GenerateWaveformBitmap(string filePath, string fallbackSeed)
        {
            int w = 340, h = 44, columns = 170;
            double[] maxPeaks = new double[columns];
            double[] rmsValues = new double[columns];

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    string ext = Path.GetExtension(filePath).ToLower();
                    using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                    if (ext == ".wav" && fs.Length > 44)
                    {
                        byte[] header = new byte[Math.Min(512, (int)fs.Length)];
                        fs.ReadAtLeast(header, header.Length, false);
                        int dataOffset = 44;
                        short bitsPerSample = BitConverter.ToInt16(header, 34);

                        for (int i = 12; i < header.Length - 8; i++)
                        {
                            if (header[i] == 'd' && header[i + 1] == 'a' && header[i + 2] == 't' && header[i + 3] == 'a')
                            {
                                dataOffset = i + 8; break;
                            }
                        }

                        long audioDataLen = Math.Max(1, fs.Length - dataOffset);
                        long stepBytes = audioDataLen / columns;
                        byte[] buf = new byte[Math.Clamp(stepBytes, 256, 8192)];

                        for (int col = 0; col < columns; col++)
                        {
                            fs.Seek(dataOffset + col * stepBytes, SeekOrigin.Begin);
                            int read = fs.ReadAtLeast(buf, buf.Length, false);
                            double peak = 0, sumSq = 0;
                            int count = 0;

                            if (bitsPerSample == 32)
                            {
                                for (int j = 0; j < read - 3; j += 4)
                                {
                                    float sample = BitConverter.ToSingle(buf, j);
                                    if (float.IsNaN(sample) || float.IsInfinity(sample)) continue;
                                    double abs = Math.Clamp(Math.Abs(sample), 0, 1.0);
                                    if (abs > peak) peak = abs;
                                    sumSq += abs * abs; count++;
                                }
                            }
                            else
                            {
                                for (int j = 0; j < read - 1; j += 2)
                                {
                                    short sample = BitConverter.ToInt16(buf, j);
                                    double abs = Math.Abs(sample / 32768.0);
                                    if (abs > peak) peak = abs;
                                    sumSq += abs * abs; count++;
                                }
                            }
                            maxPeaks[col] = peak;
                            rmsValues[col] = count > 0 ? Math.Sqrt(sumSq / count) : 0;
                        }
                    }
                    else
                    {
                        long startOffset = Math.Min(fs.Length / 10, 65536);
                        long audioDataLen = Math.Max(1, fs.Length - startOffset);
                        long stepBytes = audioDataLen / columns;
                        byte[] buf = new byte[1024];

                        for (int col = 0; col < columns; col++)
                        {
                            fs.Seek(startOffset + col * stepBytes, SeekOrigin.Begin);
                            int read = fs.ReadAtLeast(buf, buf.Length, false);
                            if (read < 4) continue;

                            double localPeak = 0, sumSq = 0;
                            for (int j = 1; j < read; j++)
                            {
                                double delta = Math.Abs(buf[j] - buf[j - 1]) / 255.0;
                                if (delta > localPeak) localPeak = delta;
                                sumSq += delta * delta;
                            }
                            maxPeaks[col] = Math.Pow(localPeak, 1.5);
                            rmsValues[col] = Math.Pow(Math.Sqrt(sumSq / read), 1.8);
                        }
                    }

                    double globalMax = maxPeaks.Max(), globalMin = maxPeaks.Min();
                    double span = Math.Max(0.001, globalMax - globalMin * 0.85);
                    for (int i = 0; i < columns; i++)
                    {
                        maxPeaks[i] = Math.Clamp((maxPeaks[i] - globalMin * 0.85) / span, 0.04, 1.0);
                        rmsValues[i] = Math.Clamp(rmsValues[i] / Math.Max(0.01, globalMax) * 0.85, 0.02, maxPeaks[i] * 0.85);
                    }
                }
                catch { }
            }

            // Draw the waveform using WPF DrawingVisual
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(16, 17, 20)), null, new Rect(0, 0, w, h));
                double centerY = h / 2.0;
                Pen peakPen = new Pen(new SolidColorBrush(Color.FromRgb(48, 112, 196)), 1.6);
                Pen rmsPen = new Pen(new SolidColorBrush(Color.FromRgb(115, 195, 255)), 1.6);
                double colWidth = (double)w / columns;

                for (int i = 0; i < columns; i++)
                {
                    if (maxPeaks[i] <= 0) continue;
                    double x = i * colWidth + colWidth / 2.0;
                    double peakHalfH = Math.Max(1.5, maxPeaks[i] * (centerY - 2));
                    double rmsHalfH = Math.Max(1.0, rmsValues[i] * (centerY - 2));
                    dc.DrawLine(peakPen, new Point(x, centerY - peakHalfH), new Point(x, centerY + peakHalfH));
                    dc.DrawLine(rmsPen, new Point(x, centerY - rmsHalfH), new Point(x, centerY + rmsHalfH));
                }
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 0.7), new Point(0, centerY), new Point(w, centerY));
            }
            RenderTargetBitmap bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            return bmp;
        }

        private void PlayPauseMedia_Click(object sender, RoutedEventArgs e)
        {
            if (SharedMediaPlayer.Source == null) { ShowToast("📂 연결된 미디어 파일이 없습니다."); return; }
            if (_isMediaPlaying) { SharedMediaPlayer.Pause(); _isMediaPlaying = false; _mediaTimer.Stop(); PlayPauseBtn.Content = "▶"; }
            else { SharedMediaPlayer.Play(); _isMediaPlaying = true; _mediaTimer.Start(); PlayPauseBtn.Content = "⏸"; }
        }

        private void StopMedia()
        {
            SharedMediaPlayer.Stop();
            _isMediaPlaying = false;
            _mediaTimer.Stop();
            PlayPauseBtn.Content = "▶";
            WaveformPlayhead.Margin = new Thickness(0);
        }

        private void StopAllActiveMedia()
        {
            try
            {
                if (SharedMediaPlayer != null) { SharedMediaPlayer.Stop(); SharedMediaPlayer.Source = null; }
                if (LightboxMediaPlayer != null) { LightboxMediaPlayer.Stop(); LightboxMediaPlayer.Source = null; }
                if (_activeInlineMediaElement != null)
                {
                    _activeInlineMediaElement.Stop();
                    _activeInlineMediaElement.Source = null;
                    _activeInlineMediaElement = null;
                }
            }
            catch { }

            foreach (var item in ReferenceList)
            {
                if (item.IsInlinePlaying) item.IsInlinePlaying = false;
            }
        }

        // Auto Loop play for Inline MediaElement (GIF/Video/Audio)
        private void InlineCardMedia_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is MediaElement me)
            {
                me.MediaEnded -= InlineCardMedia_MediaEnded;
                me.MediaEnded += InlineCardMedia_MediaEnded;
            }
        }

        private void InlineCardMedia_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (sender is MediaElement me && me.DataContext is ReferenceItem item && item.IsInlinePlaying)
            {
                me.Position = TimeSpan.Zero;
                me.Play();
            }
        }

        private void SharedMediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (SharedMediaPlayer.NaturalDuration.HasTimeSpan)
                MediaSeekSlider.Maximum = SharedMediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
        }

        private void SharedMediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            StopMedia();
            SharedMediaPlayer.Position = TimeSpan.Zero;
        }

        // --- Media Timer Tick for Updating Seek Slider and Time Display ---
        private void MediaTimer_Tick(object? sender, EventArgs e)
        {
            if (_isMediaPlaying && SharedMediaPlayer.NaturalDuration.HasTimeSpan && !_isUserSeekingMedia)
            {
                double total = SharedMediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                double cur = SharedMediaPlayer.Position.TotalSeconds;
                _isUserSeekingMedia = true; MediaSeekSlider.Value = cur; _isUserSeekingMedia = false;
                MediaTimeText.Text = $"{SharedMediaPlayer.Position:mm\\:ss} / {SharedMediaPlayer.NaturalDuration.TimeSpan:mm\\:ss}";
                if (total > 0) WaveformPlayhead.Margin = new Thickness((cur / total) * 310, 0, 0, 0);
            }

            if (_isLightboxOpen && LightboxMediaPlayer.Visibility == Visibility.Visible && LightboxMediaPlayer.NaturalDuration.HasTimeSpan && !_isUserSeekingMedia)
            {
                _isUserSeekingMedia = true; LightboxSeekSlider.Value = LightboxMediaPlayer.Position.TotalSeconds; _isUserSeekingMedia = false;
                LightboxTimeText.Text = $"{LightboxMediaPlayer.Position:mm\\:ss} / {LightboxMediaPlayer.NaturalDuration.TimeSpan:mm\\:ss}";
            }
        }

        // --- Media Seek Slider Handlers ---
        private void MediaSeekSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) { _isUserSeekingMedia = true; SeekSliderToMousePosition(MediaSeekSlider, e.GetPosition(MediaSeekSlider).X, SharedMediaPlayer, true); }
        private void MediaSeekSlider_PreviewMouseMove(object sender, MouseEventArgs e) { if (_isUserSeekingMedia && e.LeftButton == MouseButtonState.Pressed) SeekSliderToMousePosition(MediaSeekSlider, e.GetPosition(MediaSeekSlider).X, SharedMediaPlayer, true); }
        private void MediaSeekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) { SeekSliderToMousePosition(MediaSeekSlider, e.GetPosition(MediaSeekSlider).X, SharedMediaPlayer, true); _isUserSeekingMedia = false; }
        private void MediaSeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_isUserSeekingMedia && SharedMediaPlayer.NaturalDuration.HasTimeSpan) SharedMediaPlayer.Position = TimeSpan.FromSeconds(MediaSeekSlider.Value); }

        private void LightboxSeekSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) { _isUserSeekingMedia = true; SeekSliderToMousePosition(LightboxSeekSlider, e.GetPosition(LightboxSeekSlider).X, LightboxMediaPlayer, false); }
        private void LightboxSeekSlider_PreviewMouseMove(object sender, MouseEventArgs e) { if (_isUserSeekingMedia && e.LeftButton == MouseButtonState.Pressed) SeekSliderToMousePosition(LightboxSeekSlider, e.GetPosition(LightboxSeekSlider).X, LightboxMediaPlayer, false); }
        private void LightboxSeekSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) { SeekSliderToMousePosition(LightboxSeekSlider, e.GetPosition(LightboxSeekSlider).X, LightboxMediaPlayer, false); _isUserSeekingMedia = false; }
        private void LightboxSeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_isUserSeekingMedia && LightboxMediaPlayer.NaturalDuration.HasTimeSpan) LightboxMediaPlayer.Position = TimeSpan.FromSeconds(LightboxSeekSlider.Value); }

        private void SeekSliderToMousePosition(Slider slider, double mouseX, MediaElement player, bool updateWaveformHead)
        {
            if (!player.NaturalDuration.HasTimeSpan || slider.ActualWidth <= 0) return;
            double ratio = Math.Clamp(mouseX / slider.ActualWidth, 0.0, 1.0);
            double targetSeconds = ratio * player.NaturalDuration.TimeSpan.TotalSeconds;
            slider.Value = targetSeconds;
            player.Position = TimeSpan.FromSeconds(targetSeconds);
            if (updateWaveformHead) WaveformPlayhead.Margin = new Thickness(ratio * 310, 0, 0, 0);
        }

        private void WaveformBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && SharedMediaPlayer.NaturalDuration.HasTimeSpan)
            {
                Point pt = e.GetPosition(border);
                double ratio = Math.Clamp(pt.X / border.ActualWidth, 0, 1);
                SharedMediaPlayer.Position = TimeSpan.FromSeconds(ratio * SharedMediaPlayer.NaturalDuration.TimeSpan.TotalSeconds);
                WaveformPlayhead.Margin = new Thickness(ratio * 310, 0, 0, 0);
            }
        }

        // --- LightBox Mode ( LargeBox Mode ) ---
        private void GalleryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GalleryList.SelectedItem is ReferenceItem) SetLightboxMode(true);
        }

        private void SetLightboxMode(bool enable)
        {
            _isLightboxOpen = enable;
            LightboxScale.ScaleX = 1.0; LightboxScale.ScaleY = 1.0;
            StopLightboxMediaAndGif();
            StopAllActiveMedia();

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            if (enable && GalleryList.SelectedItem is ReferenceItem item)
            {
                if (RightDetailCol.Width.Value > 0 && !_isRightDrawerSlidOut)
                    _savedRightColWidth = RightDetailCol.Width;

                GalleryContainer.Visibility = Visibility.Collapsed;
                FocusEditView.Visibility = Visibility.Collapsed;
                InPageSettingsView.Visibility = Visibility.Collapsed;

                LightboxView.DataContext = item;
                LightboxView.Visibility = Visibility.Visible;

                string ext = item.FileExtension.ToLower();
                LightboxImage.Visibility = Visibility.Collapsed;
                LightboxMediaPlayer.Visibility = Visibility.Collapsed;
                LightboxMediaBar.Visibility = Visibility.Collapsed;
                LightboxTextReader.Visibility = Visibility.Collapsed;
                LightboxPdfWebView.Visibility = Visibility.Collapsed;

                if (item.ItemType == "Text" || ext == "txt" || ext == "md" || ext == "xml" || ext == "json" || ext == "cs")
                {
                    LightboxTextReader.Visibility = Visibility.Visible;
                    RenderMarkdownOrTextToViewer(item.Title, item.TextContent);
                }
                else if (ext == "pdf" && File.Exists(item.MediaFilePath))
                {
                    LightboxPdfWebView.Visibility = Visibility.Visible;
                    OpenPdfInLightboxAsync(item.MediaFilePath);
                }
                else if ((item.ItemType == "URL" || !string.IsNullOrWhiteSpace(item.WebUrl)) && item.WebUrl.StartsWith("http"))
                {
                    LightboxPdfWebView.Visibility = Visibility.Visible;
                    OpenPdfInLightboxAsync(item.WebUrl);
                }
                else if ((ext == "mp4" || ext == "webm" || ext == "avi" || ext == "mov" || item.ItemType == "Audio") && File.Exists(item.MediaFilePath))
                {
                    LightboxMediaPlayer.Visibility = Visibility.Visible;
                    LightboxMediaBar.Visibility = Visibility.Visible;
                    LightboxMediaPlayer.Source = new Uri(item.MediaFilePath, UriKind.Absolute);
                    LightboxMediaPlayer.Play();
                    LightboxPlayBtn.Content = "⏸"; 
                    _mediaTimer.Start();
                }
                else if (ext == "gif" && File.Exists(item.CoverImagePath))
                {
                    LightboxImage.Visibility = Visibility.Visible;
                    StartGifAnimation(item.CoverImagePath);
                }
                else if (!string.IsNullOrEmpty(item.CoverImagePath) && File.Exists(item.CoverImagePath))
                {
                    LightboxImage.Source = LoadUnlockedBitmap(item.CoverImagePath);
                    LightboxImage.Visibility = Visibility.Visible;
                }

                LightboxPopupScale.ScaleX = 0.94; LightboxPopupScale.ScaleY = 0.94;
                LightboxView.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
                LightboxPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.94, 1.0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
                LightboxPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.94, 1.0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });

                RightSplitterCol.Width = new GridLength(0);
                RightSplitter.Visibility = Visibility.Collapsed;
                RightDetailCol.MinWidth = 0;
                RightDetailCol.Width = new GridLength(0);

                _isRightDrawerSlidOut = false;
                RightDetailBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                var closeAnim = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };
                closeAnim.Completed += (s, e) =>
                {
                    if (!_isLightboxOpen)
                    {
                        LightboxView.Visibility = Visibility.Collapsed;
                        GalleryContainer.Visibility = Visibility.Visible;

                        // Optimized cleanup for WebView2 to prevent memory leaks
                        if (LightboxPdfWebView.CoreWebView2 != null)
                        {
                            LightboxPdfWebView.CoreWebView2.Navigate("about:blank");
                        }
                        LightboxPdfWebView.Visibility = Visibility.Collapsed;
                        GC.Collect(1, GCCollectionMode.Optimized, false);
                    }
                };
                LightboxView.BeginAnimation(UIElement.OpacityProperty, closeAnim);
                LightboxPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.95, TimeSpan.FromMilliseconds(160)));
                LightboxPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.95, TimeSpan.FromMilliseconds(160)));

                _isRightDrawerSlidOut = false;
                Grid.SetColumn(RightDetailBorder, 4);
                Grid.SetColumnSpan(RightDetailBorder, 1);
                RightDetailBorder.Width = double.NaN;
                RightDetailBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
                if (!_isRightSidebarCollapsed)
                {
                    RightDetailBorder.Visibility = Visibility.Visible;
                    RightSplitter.Visibility = Visibility.Visible;
                    RightSplitterCol.Width = new GridLength(5);
                    RightDetailCol.MinWidth = 330;
                    RightDetailCol.Width = _savedRightColWidth;
                    RightDrawerTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
                }
            }
        }

        // Fixed issue with BitmapImage locking the file by using OnLoad and Freeze
        private BitmapImage? LoadUnlockedBitmap(string filePath)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(filePath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        private async void OpenPdfInLightboxAsync(string pdfFilePath)
        {
            try
            {
                await LightboxPdfWebView.EnsureCoreWebView2Async();
                LightboxPdfWebView.CoreWebView2.Navigate(new Uri(pdfFilePath, UriKind.Absolute).AbsoluteUri);
            }
            catch (Exception ex) { ShowToast($"⚠️ PDF Error: {ex.Message}"); }
        }

        private void StartGifAnimation(string gifPath)
        {
            try
            {
                using FileStream fs = new FileStream(gifPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                GifBitmapDecoder decoder = new GifBitmapDecoder(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                _gifFrames = decoder.Frames.Cast<BitmapSource>().ToList();
                if (_gifFrames.Count > 0)
                {
                    _currentGifFrameIndex = 0;
                    LightboxImage.Source = _gifFrames[0];
                    _gifTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(85) };
                    _gifTimer.Tick += (s, e) =>
                    {
                        if (_gifFrames == null || _gifFrames.Count == 0) return;
                        _currentGifFrameIndex = (_currentGifFrameIndex + 1) % _gifFrames.Count;
                        LightboxImage.Source = _gifFrames[_currentGifFrameIndex];
                    };
                    _gifTimer.Start();
                }
            }
            catch { }
        }
        private void StopLightboxMediaAndGif()
        {
            _gifTimer?.Stop();
            _gifFrames = null;
            LightboxMediaPlayer.Stop();
            LightboxMediaPlayer.Source = null;
            if (LightboxPdfWebView != null && LightboxPdfWebView.CoreWebView2 != null)
                LightboxPdfWebView.CoreWebView2.Navigate("about:blank");
        }

        private void LightboxPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (LightboxPlayBtn.Content.ToString() == "⏸") { LightboxMediaPlayer.Pause(); LightboxPlayBtn.Content = "▶"; }
            else { LightboxMediaPlayer.Play(); LightboxPlayBtn.Content = "⏸"; }
        } 

        private void LightboxMediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (LightboxMediaPlayer.NaturalDuration.HasTimeSpan)
                LightboxSeekSlider.Maximum = LightboxMediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
        }

        private void LightboxMediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            LightboxMediaPlayer.Position = TimeSpan.Zero;
            LightboxMediaPlayer.Play();
        }

        // --- Right Drawer Slide In/Out in Lightbox Mode ( UX ) ---
        private void ShowRightDrawerInLightbox()
        {
            if (_isRightDrawerSlidOut) return;
            _isRightDrawerSlidOut = true;

            if (LightboxPdfWebView.Visibility == Visibility.Visible)
                LightboxPdfWebView.Margin = new Thickness(0, 0, 410, 0);

            Grid.SetColumn(RightDetailBorder, 2);
            Grid.SetColumnSpan(RightDetailBorder, 3);
            RightDetailBorder.Width = 410;
            RightDetailBorder.HorizontalAlignment = HorizontalAlignment.Right;
            RightDetailBorder.Visibility = Visibility.Visible;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            RightDrawerTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(410, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        }

        private void HideRightDrawerInLightbox()
        {
            if (!_isRightDrawerSlidOut) return;
            _isRightDrawerSlidOut = false;

            if (LightboxPdfWebView.Visibility == Visibility.Visible)
                LightboxPdfWebView.Margin = new Thickness(0);

            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            var slideOut = new DoubleAnimation(410, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease };
            slideOut.Completed += (s, e) =>
            {
                if (!_isRightDrawerSlidOut && _isLightboxOpen)
                    RightDetailBorder.Visibility = Visibility.Collapsed;
            };
            RightDrawerTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
        }

        // --- Mouse Move Handler for Lightbox Right Drawer Slide In/Out ---
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isLightboxOpen) return;
            Point pt = e.GetPosition(this);
            double distFromRightEdge = this.ActualWidth - pt.X;

            if (!_isRightDrawerSlidOut && distFromRightEdge <= 45)
                ShowRightDrawerInLightbox();
            else if (_isRightDrawerSlidOut && distFromRightEdge > 430)
            {
                HideRightDrawerInLightbox();
                SaveAllData();
            }
        }

        private void LightboxImage_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double step = e.Delta > 0 ? 0.15 : -0.15;
            double next = Math.Clamp(LightboxScale.ScaleX + step, 0.5, 4.0);
            LightboxScale.ScaleX = next; LightboxScale.ScaleY = next;
            e.Handled = true;
        }

        private void LightboxBackground_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) SetLightboxMode(false);
        }

        private void CloseLightbox_Click(object sender, RoutedEventArgs e) => SetLightboxMode(false);

        private void OpenFullEditFromLightbox_Click(object sender, RoutedEventArgs e)
        {
            StopLightboxMediaAndGif();
            StopMedia();
            _isLightboxOpen = false;
            LightboxView.Visibility = Visibility.Collapsed;
            SetFocusEditMode(true);
        }

        private void CloseFocusMode_Click(object sender, RoutedEventArgs e) => SetFocusEditMode(false);

        // --- Focus Edit Mode ( Fullscreen Edit Mode ) ---
        private void SetFocusEditMode(bool enable)
        {
            StopLightboxMediaAndGif();
            StopMedia();
            _isLightboxOpen = false;
            LightboxView.Visibility = Visibility.Collapsed;
            InPageSettingsView.Visibility = Visibility.Collapsed;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            if (enable)
            {
                if (RightDetailCol.Width.Value > 0 && !_isRightDrawerSlidOut)
                    _savedRightColWidth = RightDetailCol.Width;

                GalleryContainer.Visibility = Visibility.Collapsed;
                FocusEditView.Visibility = Visibility.Visible;

                FocusViewScale.ScaleX = 0.96; FocusViewScale.ScaleY = 0.96;
                FocusEditView.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
                FocusViewScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
                FocusViewScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });

                RightDetailCol.MinWidth = 0; RightDetailCol.Width = new GridLength(0);
                RightSplitterCol.Width = new GridLength(0);
                RightDetailBorder.Visibility = Visibility.Collapsed;
                RightSplitter.Visibility = Visibility.Collapsed;
            }
            else
            {
                FocusEditView.Visibility = Visibility.Collapsed;
                GalleryContainer.Visibility = Visibility.Visible;

                Grid.SetColumn(RightDetailBorder, 4);
                Grid.SetColumnSpan(RightDetailBorder, 1);
                RightDetailBorder.Width = double.NaN;
                RightDetailBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
                if (!_isRightSidebarCollapsed)
                {
                    RightDetailBorder.Visibility = Visibility.Visible;
                    RightSplitter.Visibility = Visibility.Visible;
                    RightSplitterCol.Width = new GridLength(5);
                    RightDetailCol.MinWidth = 330;
                    RightDetailCol.Width = _savedRightColWidth;
                    RightDrawerTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(150)));
                }
                SaveAllData();
            }
        }

        // --- Global Key Handlers ---
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (InPageSettingsView.Visibility == Visibility.Visible) { CloseSettings_Click(sender, e); e.Handled = true; return; }
                if (SearchFilterPopup.IsOpen) { SearchFilterPopup.IsOpen = false; e.Handled = true; return; }
                if (UniversalColorPickerPopup.IsOpen) { UniversalColorPickerPopup.IsOpen = false; e.Handled = true; return; }
                if (TagManagerModalOverlay.Visibility == Visibility.Visible) { TagManagerModalOverlay.Visibility = Visibility.Collapsed; e.Handled = true; return; }
                if (LightboxView.Visibility == Visibility.Visible) { SetLightboxMode(false); e.Handled = true; return; }
                if (FocusEditView.Visibility == Visibility.Visible) { SetFocusEditMode(false); e.Handled = true; return; }
            }

            if (Keyboard.FocusedElement is TextBox) return;

            // Ctrl + A to All select
            if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                GalleryList.SelectAll();
                ShowToast($"✅ 전체 {GalleryList.SelectedItems.Count}개 항목 선택됨");
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Space && GalleryList.SelectedItem is ReferenceItem)
            {
                if (!_isLightboxOpen && FocusEditView.Visibility != Visibility.Visible)
                    SetLightboxMode(true);
                else if (_isLightboxOpen)
                    OpenFullEditFromLightbox_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (_isLightboxOpen && LightboxMediaPlayer.Visibility == Visibility.Visible && LightboxMediaPlayer.NaturalDuration.HasTimeSpan)
            {
                if (e.Key == Key.Right)
                {
                    LightboxMediaPlayer.Position = TimeSpan.FromSeconds(Math.Min(LightboxMediaPlayer.NaturalDuration.TimeSpan.TotalSeconds, LightboxMediaPlayer.Position.TotalSeconds + 5));
                    e.Handled = true; return;
                }
                if (e.Key == Key.Left)
                {
                    LightboxMediaPlayer.Position = TimeSpan.FromSeconds(Math.Max(0, LightboxMediaPlayer.Position.TotalSeconds - 5));
                    e.Handled = true; return;
                }
            }

            if (e.Key == Key.Delete && GalleryList.SelectedItem is ReferenceItem selected)
            {
                bool isShiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                ExecuteDeleteItem(selected, isShiftPressed);
                e.Handled = true;
            }

            // Ctrl + Z 
            if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                UndoRemovedTag_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
        }

        // --- ShowToast & Lerp Scrolling & Marquee Selection ---
        private void ShowToast(string message)
        {
            ToastText.Text = message;
            DoubleAnimation fadeAnim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180));
            DoubleAnimation slideAnim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            fadeAnim.Completed += async (s, e) =>
            {
                await Task.Delay(1300);
                ToastNotification.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(350)));
                ToastTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(20, TimeSpan.FromMilliseconds(350)));
            };
            ToastNotification.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            ToastTransform.BeginAnimation(TranslateTransform.YProperty, slideAnim);
        }

        private ScrollViewer? GetGalleryScrollViewer()
        {
            if (_galleryScrollViewer != null) return _galleryScrollViewer;
            _galleryScrollViewer = FindVisualChild<ScrollViewer>(GalleryList);
            return _galleryScrollViewer;
        }

        private void OnCompositionTargetRendering(object? sender, EventArgs e)
        {
            if (_isLerpScrolling)
            {
                var sv = GetGalleryScrollViewer();
                if (sv != null)
                {
                    _currentVerticalOffset += (_targetVerticalOffset - _currentVerticalOffset) * 0.18;
                    if (Math.Abs(_targetVerticalOffset - _currentVerticalOffset) < 0.4) { _currentVerticalOffset = _targetVerticalOffset; _isLerpScrolling = false; }
                    sv.ScrollToVerticalOffset(_currentVerticalOffset);
                }
            }

            if (_isLeftLerpScrolling && LeftSidebarScrollViewer != null)
            {
                _leftCurrentVerticalOffset += (_leftTargetVerticalOffset - _leftCurrentVerticalOffset) * 0.18;
                if (Math.Abs(_leftTargetVerticalOffset - _leftCurrentVerticalOffset) < 0.4) { _leftCurrentVerticalOffset = _leftTargetVerticalOffset; _isLeftLerpScrolling = false; }
                LeftSidebarScrollViewer.ScrollToVerticalOffset(_leftCurrentVerticalOffset);
            }
        }

        private void LeftSidebar_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            if (!_isLeftLerpScrolling) _leftCurrentVerticalOffset = LeftSidebarScrollViewer.VerticalOffset;
            _leftTargetVerticalOffset = Math.Clamp(_leftTargetVerticalOffset - (e.Delta * 1.0), 0, LeftSidebarScrollViewer.ScrollableHeight);
            _isLeftLerpScrolling = true;
        }

        private void LeftSidebar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var hitItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
            var hitTreeItem = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (hitItem == null && hitTreeItem == null)
            {
                _isLeftPanning = true;
                _leftPanStartPoint = e.GetPosition(LeftSidebarScrollViewer);
                _leftPanStartOffset = LeftSidebarScrollViewer.VerticalOffset;
                _leftCurrentVerticalOffset = _leftPanStartOffset;
                _leftTargetVerticalOffset = _leftPanStartOffset;
                LeftSidebarScrollViewer.Cursor = Cursors.ScrollNS;
            }
        }

        private void LeftSidebar_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isLeftPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPt = e.GetPosition(LeftSidebarScrollViewer);
                double deltaY = _leftPanStartPoint.Y - currentPt.Y;
                _leftTargetVerticalOffset = Math.Clamp(_leftPanStartOffset + deltaY * 1.2, 0, LeftSidebarScrollViewer.ScrollableHeight);
                _isLeftLerpScrolling = true;
            }
            else if (_isLeftPanning) { _isLeftPanning = false; LeftSidebarScrollViewer.Cursor = Cursors.Arrow; }
        }

        private void LeftSidebar_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isLeftPanning = false;
            LeftSidebarScrollViewer.Cursor = Cursors.Arrow;
        }

        private void BubbleMouseWheelToParent(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = sender };
                ((Control)sender).Parent.As<UIElement>()?.RaiseEvent(eventArg);
            }
        }

        private void GalleryList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                CardSizeSlider.Value = Math.Clamp(CardSizeSlider.Value + (e.Delta > 0 ? 15 : -15), CardSizeSlider.Minimum, CardSizeSlider.Maximum);
                return;
            }

            var sv = GetGalleryScrollViewer();
            if (sv != null)
            {
                e.Handled = true;
                if (!_isLerpScrolling) _currentVerticalOffset = sv.VerticalOffset;
                _targetVerticalOffset = Math.Clamp(_targetVerticalOffset - (e.Delta * 1.1), 0, sv.ScrollableHeight);
                _isLerpScrolling = true;
            }
        }

        private void GalleryList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _galleryDragStartPt = e.GetPosition(this);
            var item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
            _draggingGalleryCard = item?.DataContext as ReferenceItem;
            _isGalleryDraggingActive = false;
            _hoveredDropTargetFolder = null;
        }

        private void GalleryList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                if (!_isGalleryDraggingActive) HideGhostPopup();
                return;
            }
            if (_draggingGalleryCard is not ReferenceItem sourceItem) return;

            Point curWinPt = e.GetPosition(this);
            if (!_isGalleryDraggingActive && (Math.Abs(curWinPt.X - _galleryDragStartPt.X) > 6 || Math.Abs(curWinPt.Y - _galleryDragStartPt.Y) > 6))
            {
                _isGalleryDraggingActive = true;
                _hoveredDropTargetFolder = null;
                GalleryList.CaptureMouse(); // 마우스가 좌측 폴더 트리로 넘어가도 끝까지 추적!
            }

            if (_isGalleryDraggingActive)
            {
                // 1. 마우스가 좌측 폴더 트리(UserFolderTree)의 폴더 위에 있는지 실시간 감지
                _hoveredDropTargetFolder = GetFolderNodeAtMouse(e);
                if (_hoveredDropTargetFolder != null)
                {
                    bool isMultiCopy = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                    string actionText = isMultiCopy
                        ? Loc($"➕ '{_hoveredDropTargetFolder.Name}' にも同時追加 (Multi)", $"➕ '{_hoveredDropTargetFolder.Name}' 폴더에 동시 소속 추가 (Ctrl)", $"➕ Also add to '{_hoveredDropTargetFolder.Name}' (Multi)")
                        : Loc($"📁 '{_hoveredDropTargetFolder.Name}' へ移動 (Ctrl: 同時追加)", $"📁 '{_hoveredDropTargetFolder.Name}' 폴더로 이동 (Ctrl 누르면 동시 소속)", $"📁 Move to '{_hoveredDropTargetFolder.Name}' (Hold Ctrl to Multi-add)");

                    UpdateGhostPosition(curWinPt, actionText);
                    return;
                }

                // 2. 갤러리 내부에서 드래그 중일 때 (수동 정렬 순서 변경)
                UpdateGhostPosition(curWinPt, $"🖼️ {sourceItem.Title}");

                if (_currentSortMode == 0)
                {
                    var hit = GalleryList.InputHitTest(e.GetPosition(GalleryList)) as DependencyObject;
                    var targetContainer = FindAncestor<ListBoxItem>(hit);
                    if (targetContainer?.DataContext is ReferenceItem targetItem && sourceItem != targetItem)
                    {
                        int oldIdx = ReferenceList.IndexOf(sourceItem);
                        int newIdx = ReferenceList.IndexOf(targetItem);
                        if (oldIdx >= 0 && newIdx >= 0)
                        {
                            ReferenceList.Move(oldIdx, newIdx);
                            GalleryList.SelectedItem = sourceItem;
                        }
                    }
                }
            }
        }

        private void GalleryList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            bool wasDragging = _isGalleryDraggingActive;
            var droppedCard = _draggingGalleryCard as ReferenceItem;
            FolderNode? targetFolder = _hoveredDropTargetFolder ?? GetFolderNodeAtMouse(e);

            if (GalleryList.IsMouseCaptured) GalleryList.ReleaseMouseCapture();

            _isGalleryDraggingActive = false;
            _draggingGalleryCard = null;
            _hoveredDropTargetFolder = null;
            HideGhostPopup();

            if (!wasDragging || droppedCard == null) return;

            // 좌측 폴더 위에 드롭한 경우: 단일 이동 vs 다중 소속(Ctrl) 분기 처리!
            if (targetFolder != null)
            {
                if (targetFolder.IsLocked)
                {
                    ShowToast(Loc("🔒 ロックされたフォルダです。", "🔒 잠긴 폴더에는 넣을 수 없습니다!", "🔒 Folder is locked!"));
                    return;
                }

                bool isMultiClassify = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                var selectedItems = GalleryList.SelectedItems.Cast<ReferenceItem>().ToList();
                if (!selectedItems.Contains(droppedCard)) selectedItems = new List<ReferenceItem> { droppedCard };

                foreach (var item in selectedItems)
                {
                    if (item.IsLocked) continue;
                    item.IsDeleted = false;

                    if (isMultiClassify)
                    {
                        // [다중 소속] 기존 폴더들을 유지한 채 새 폴더에도 동시 등록!
                        if (!string.IsNullOrWhiteSpace(item.FolderName) && !item.FolderNames.Contains(item.FolderName))
                            item.FolderNames.Add(item.FolderName);

                        item.AddToFolder(targetFolder.Name);
                    }
                    else
                    {
                        // [단순 이동] 기존 소속을 비우고 새 폴더로 완전히 이동!
                        item.FolderNames.Clear();
                        item.FolderName = targetFolder.Name;
                    }
                }

                SaveAllData();
                CollectionViewSource.GetDefaultView(GalleryList.ItemsSource)?.Refresh();
                GalleryList.Items.Refresh();

                ShowToast(isMultiClassify
                    ? Loc($"➕ {selectedItems.Count}個のアイテムを '{targetFolder.Name}' にも同時追加しました！", $"➕ {selectedItems.Count}개 항목을 기존 폴더 유지 + '{targetFolder.Name}'에 동시 추가!", $"➕ Added {selectedItems.Count} items to '{targetFolder.Name}' (Multi-Folder)!")
                    : Loc($"📁 {selectedItems.Count}個のアイテムを '{targetFolder.Name}' へ移動しました！", $"📁 {selectedItems.Count}개 항목을 '{targetFolder.Name}' 폴더로 이동 완료!", $"📁 Moved {selectedItems.Count} items to '{targetFolder.Name}'!"));
            }
            else
            {
                SaveAllData();
            }
        }

        private FolderNode? GetFolderNodeAtMouse(MouseEventArgs e)
        {
            foreach (var container in FindVisualChildren<TreeViewItem>(UserFolderTree))
            {
                if (!container.IsVisible) continue;
                var header = container.Template?.FindName("ItemRowBorder", container) as FrameworkElement ?? container;
                if (header.ActualWidth <= 0 || header.ActualHeight <= 0) continue;

                Point pt = e.GetPosition(header);
                if (pt.X >= 0 && pt.Y >= 0 && pt.X <= header.ActualWidth && pt.Y <= header.ActualHeight)
                {
                    if (container.DataContext is FolderNode folder)
                        return folder;
                }
            }
            return null;
        }

        // Locked Folder Handling
        private void MoveFolderToRoot_Click(object sender, RoutedEventArgs e)
        {
            if (UserFolderTree.SelectedItem is FolderNode targetFolder)
            {
                if (targetFolder.IsLocked)
                {
                    ShowToast(Loc("🔒 ロックされたフォルダです。", "🔒 잠긴 폴더는 이동할 수 없습니다!", "🔒 Folder is locked!"));
                    return;
                }
                RemoveFolderRecursive(FolderList, targetFolder);
                FolderList.Add(targetFolder);
                SaveFolders();
                ShowToast(Loc("📤 ルートフォルダへ移動しました", "📤 최상위 폴더로 이동되었습니다.", "📤 Moved to root level."));
            }
        }

        private void GalleryList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ReFanS_CardItem") && e.Data.GetData("ReFanS_CardItem") is ReferenceItem sourceItem)
            {
                var hit = GalleryList.InputHitTest(e.GetPosition(GalleryList)) as DependencyObject;
                var targetContainer = FindAncestor<ListBoxItem>(hit);

                if (targetContainer?.DataContext is ReferenceItem targetItem && sourceItem != targetItem)
                {
                    // if auto sorting (date/name/etc.) was enabled, automatically switch to 'manual sort (0)' before placement
                    if (_currentSortMode != 0)
                    {
                        _currentSortMode = 0;
                        ICollectionView view = CollectionViewSource.GetDefaultView(GalleryList.ItemsSource);
                        view?.SortDescriptions.Clear();
                        UpdateSortButtonText();
                        SaveUserPreferences();
                    }

                    int oldIdx = ReferenceList.IndexOf(sourceItem);
                    int newIdx = ReferenceList.IndexOf(targetItem);
                    if (oldIdx >= 0 && newIdx >= 0)
                    {
                        ReferenceList.Move(oldIdx, newIdx);
                        GalleryList.SelectedItem = sourceItem;
                        SaveAllData();
                    }
                }
            }
        }

        private void SortMenuBtn_Click(object sender, RoutedEventArgs e)
        {
            if (SortMenuBtn.ContextMenu != null)
            {
                SortMenuBtn.ContextMenu.PlacementTarget = SortMenuBtn;
                SortMenuBtn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                SortMenuBtn.ContextMenu.IsOpen = true;
            }
        }

        private void SortOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && int.TryParse(menuItem.Tag?.ToString(), out int sortMode))
            {
                _currentSortMode = sortMode;
                UpdateSortButtonText();

                if (GalleryList?.ItemsSource == null) return;
                ICollectionView view = CollectionViewSource.GetDefaultView(GalleryList.ItemsSource);
                if (view == null) return;

                view.SortDescriptions.Clear();
                switch (sortMode)
                {
                    case 0: break;
                    case 1: view.SortDescriptions.Add(new SortDescription(nameof(ReferenceItem.CreatedAt), ListSortDirection.Descending)); break;
                    case 2: view.SortDescriptions.Add(new SortDescription(nameof(ReferenceItem.Title), ListSortDirection.Ascending)); break;
                    case 3:
                        view.SortDescriptions.Add(new SortDescription(nameof(ReferenceItem.ItemType), ListSortDirection.Ascending));
                        view.SortDescriptions.Add(new SortDescription(nameof(ReferenceItem.Title), ListSortDirection.Ascending));
                        break;
                }
                view.Refresh();
                GalleryList.Items.Refresh();
                ShowToast($"⇅ '{menuItem.Header.ToString()?.Trim()}'");
            }
        }
        private void UpdateSortButtonText()
        {
            if (SortMenuBtn.Template?.FindName("CurrentSortText", SortMenuBtn) is TextBlock labelText)
            {
                labelText.Text = (_currentLang, _currentSortMode) switch
                {
                    ("JA", 1) => "登録日時順",
                    ("JA", 2) => "名前順",
                    ("JA", 3) => "種類・拡張子順",
                    ("JA", _) => "手動並び替え",

                    ("EN", 1) => "Date Added",
                    ("EN", 2) => "Name (A-Z)",
                    ("EN", 3) => "File Type",
                    ("EN", _) => "Manual Sort",

                    (_, 1) => "날짜/시간순",
                    (_, 2) => "이름순",
                    (_, 3) => "확장자/유형별",
                    _ => "수동 정렬"
                };
            }
        }

        // --- Tag Cloud Generation ---
        private double GetRainbowHueSortKey(string hexColor)
        {
            try
            {
                Color c = (Color)ColorConverter.ConvertFromString(hexColor);
                double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
                double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
                if (delta < 0.05) return 400 + (1.0 - max) * 100;
                double hue = 0;
                if (max == r) hue = 60 * (((g - b) / delta) % 6);
                else if (max == g) hue = 60 * (((b - r) / delta) + 2);
                else if (max == b) hue = 60 * (((r - g) / delta) + 4);
                if (hue < 0) hue += 360;
                return hue;
            }
            catch { return 999; }
        }

        private void RefreshTagCloud()
        {
            if (FilterTreeNodes == null) return;

            var expandedStates = FilterTreeNodes.ToDictionary(n => n.DisplayName, n => (n.IsExpanded, n.IconColor, n.NameColor));
            FilterTreeNodes.Clear();

            var activeItems = ReferenceList.Where(i => !i.IsDeleted).ToList();

            foreach (var itemTag in activeItems.SelectMany(i => i.Labels))
            {
                if (!MasterTagCatalog.Any(m => m.Name.Equals(itemTag.Name, StringComparison.OrdinalIgnoreCase)))
                    MasterTagCatalog.Add(new LabelTag { Name = itemTag.Name, HexColor = itemTag.HexColor });
            }

            var tagGroupHeader = CreateHeaderNode("🏷️ TAGS", expandedStates, true);
            var rootTagDict = new Dictionary<string, FilterTreeNode>(StringComparer.OrdinalIgnoreCase);

            foreach (var mTag in MasterTagCatalog)
            {
                if (string.IsNullOrWhiteSpace(mTag.Name)) continue;
                string[] parts = mTag.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);
                string rootName = parts[0].Trim();

                if (!rootTagDict.TryGetValue(rootName, out var rootNode))
                {
                    rootNode = new FilterTreeNode { DisplayName = rootName, FullMatchKey = rootName, CategoryType = "Tag", HexColor = mTag.HexColor, HueSortKey = GetRainbowHueSortKey(mTag.HexColor), IsExpanded = true };
                    rootTagDict[rootName] = rootNode;
                }

                if (parts.Length > 1)
                {
                    string subName = string.Join("/", parts.Skip(1)).Trim();
                    if (!rootNode.SubNodes.Any(s => s.DisplayName.Equals(subName, StringComparison.OrdinalIgnoreCase)))
                        rootNode.SubNodes.Add(new FilterTreeNode { DisplayName = subName, FullMatchKey = mTag.Name.Trim(), CategoryType = "TagExact", HexColor = mTag.HexColor, HueSortKey = GetRainbowHueSortKey(mTag.HexColor) });
                }
            }

            foreach (var root in rootTagDict.Values)
            {
                root.ItemCount = activeItems.Count(i => i.Labels.Any(l => l.Name.Equals(root.FullMatchKey, StringComparison.OrdinalIgnoreCase) || l.Name.StartsWith(root.FullMatchKey + "/", StringComparison.OrdinalIgnoreCase)));
                foreach (var sub in root.SubNodes)
                    sub.ItemCount = activeItems.Count(i => i.Labels.Any(l => l.Name.Equals(sub.FullMatchKey, StringComparison.OrdinalIgnoreCase)));
            }

            foreach (var root in rootTagDict.Values.OrderBy(n => n.HueSortKey).ThenBy(n => n.DisplayName))
            {
                var sortedSubs = root.SubNodes.OrderBy(s => s.HueSortKey).ThenBy(s => s.DisplayName).ToList();
                root.SubNodes.Clear();
                foreach (var s in sortedSubs) root.SubNodes.Add(s);
                tagGroupHeader.SubNodes.Add(root);
            }
            tagGroupHeader.ItemCount = tagGroupHeader.SubNodes.Count;
            FilterTreeNodes.Add(tagGroupHeader);

            foreach (var customGroup in CustomFilterGroups)
            {
                var customHeader = CreateHeaderNode($"📌 {customGroup}", expandedStates, true);
                foreach (var matchingRoot in rootTagDict.Values.Where(r => r.FullMatchKey.Equals(customGroup, StringComparison.OrdinalIgnoreCase)))
                    foreach (var sub in matchingRoot.SubNodes) customHeader.SubNodes.Add(sub);
                customHeader.ItemCount = customHeader.SubNodes.Count;
                FilterTreeNodes.Add(customHeader);
            }

            var composerHeader = CreateHeaderNode("🎼 Composer", expandedStates, false);
            foreach (var grp in activeItems.Where(i => !string.IsNullOrWhiteSpace(i.Composer)).GroupBy(i => i.Composer))
            {
                var cNode = new FilterTreeNode { DisplayName = grp.Key, FullMatchKey = grp.Key, CategoryType = "Composer", HexColor = "#BAE1FF", ItemCount = grp.Count() };
                foreach (var gameGrp in grp.Where(i => !string.IsNullOrWhiteSpace(i.GameTitle)).GroupBy(i => i.GameTitle))
                    cNode.SubNodes.Add(new FilterTreeNode { DisplayName = $"🎮 {gameGrp.Key}", FullMatchKey = $"{grp.Key}|{gameGrp.Key}", CategoryType = "ComposerGame", HexColor = "#E2F0CB", ItemCount = gameGrp.Count() });
                composerHeader.SubNodes.Add(cNode);
            }
            composerHeader.ItemCount = composerHeader.SubNodes.Count;
            FilterTreeNodes.Add(composerHeader);

            var creatorHeader = CreateHeaderNode("🎤 Artist", expandedStates, false);
            foreach (var grp in activeItems.Where(i => !string.IsNullOrWhiteSpace(i.Creator)).GroupBy(i => i.Creator))
                creatorHeader.SubNodes.Add(new FilterTreeNode { DisplayName = grp.Key, FullMatchKey = grp.Key, CategoryType = "Creator", HexColor = "#FFDFBA", ItemCount = grp.Count() });
            creatorHeader.ItemCount = creatorHeader.SubNodes.Count;
            FilterTreeNodes.Add(creatorHeader);

            var gameHeader = CreateHeaderNode("🎮 Game / Title", expandedStates, false);
            foreach (var grp in activeItems.Where(i => !string.IsNullOrWhiteSpace(i.GameTitle)).GroupBy(i => i.GameTitle))
                gameHeader.SubNodes.Add(new FilterTreeNode { DisplayName = grp.Key, FullMatchKey = grp.Key, CategoryType = "Game", HexColor = "#BAFFC9", ItemCount = grp.Count() });
            gameHeader.ItemCount = gameHeader.SubNodes.Count;
            FilterTreeNodes.Add(gameHeader);

            var companyHeader = CreateHeaderNode("🏢 Company / Publisher", expandedStates, false);
            foreach (var grp in activeItems.Where(i => !string.IsNullOrWhiteSpace(i.Company)).GroupBy(i => i.Company))
                companyHeader.SubNodes.Add(new FilterTreeNode { DisplayName = grp.Key, FullMatchKey = grp.Key, CategoryType = "Company", HexColor = "#E0BBE4", ItemCount = grp.Count() });
            companyHeader.ItemCount = companyHeader.SubNodes.Count;
            FilterTreeNodes.Add(companyHeader);

            var colorHeader = CreateHeaderNode("🎨 Colors Detection", expandedStates, false);
            var colors = activeItems.SelectMany(i => i.ColorPalette).Distinct()
                                    .Select(hex => new FilterTreeNode { DisplayName = hex, FullMatchKey = hex, CategoryType = "Color", HexColor = hex, HueSortKey = GetRainbowHueSortKey(hex), ItemCount = activeItems.Count(item => item.ColorPalette.Any(c => IsColorSimilar(c, hex, 65.0))) })
                                    .OrderBy(n => n.HueSortKey).Take(35);
            foreach (var cNode in colors) colorHeader.SubNodes.Add(cNode);
            colorHeader.ItemCount = colorHeader.SubNodes.Count;
            FilterTreeNodes.Add(colorHeader);

            var fontHeader = CreateHeaderNode("🔤 Fonts", expandedStates, false);
            foreach (var f in activeItems.SelectMany(i => i.DetectedFonts).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
            {
                int cnt = activeItems.Count(i => i.DetectedFonts.Contains(f));
                fontHeader.SubNodes.Add(new FilterTreeNode { DisplayName = f, FullMatchKey = f, CategoryType = "Font", HexColor = "#F6EAC2", ItemCount = cnt });
            }
            fontHeader.ItemCount = fontHeader.SubNodes.Count;
            FilterTreeNodes.Add(fontHeader);
        }

        private FilterTreeNode CreateHeaderNode(string title, Dictionary<string, (bool exp, string iconCol, string nameCol)> states, bool defaultExp)
        {
            if (states.TryGetValue(title, out var st))
                return new FilterTreeNode { DisplayName = title, CategoryType = "Header", IsCategoryHeader = true, IsExpanded = st.exp, IconColor = st.iconCol, NameColor = st.nameCol };
            return new FilterTreeNode { DisplayName = title, CategoryType = "Header", IsCategoryHeader = true, IsExpanded = defaultExp };
        }

        // --- Filter TreeView Selection Handling ---
        private void FilterTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (FilterTreeView.SelectedItem is FilterTreeNode clicked)
            {
                if (clicked.IsCategoryHeader) { clicked.IsExpanded = !clicked.IsExpanded; return; }
                SetFocusEditMode(false); SetLightboxMode(false); CloseSettings_Click(sender, e);
                SmartFolderList.UnselectAll();

                ApplyFilter(item =>
                {
                    if (item.IsDeleted) return false;
                    return clicked.CategoryType switch
                    {
                        "Tag" => item.Labels.Any(l => l.Name.Equals(clicked.FullMatchKey, StringComparison.OrdinalIgnoreCase) || l.Name.StartsWith(clicked.FullMatchKey + "/", StringComparison.OrdinalIgnoreCase)),
                        "TagExact" => item.Labels.Any(l => l.Name.Equals(clicked.FullMatchKey, StringComparison.OrdinalIgnoreCase)),
                        "Composer" => item.Composer.Equals(clicked.FullMatchKey, StringComparison.OrdinalIgnoreCase),
                        "ComposerGame" => $"{item.Composer}|{item.GameTitle}".Equals(clicked.FullMatchKey, StringComparison.OrdinalIgnoreCase),
                        "Creator" => item.Creator.Equals(clicked.FullMatchKey, StringComparison.OrdinalIgnoreCase),
                        "Game" => item.GameTitle.Equals(clicked.FullMatchKey, StringComparison.OrdinalIgnoreCase),
                        "Company" => item.Company.Equals(clicked.FullMatchKey, StringComparison.OrdinalIgnoreCase),
                        "Color" => item.ColorPalette.Any(c => IsColorSimilar(c, clicked.HexColor, 65.0)),
                        "Font" => item.DetectedFonts.Any(f => f.Equals(clicked.FullMatchKey, StringComparison.OrdinalIgnoreCase)),
                        _ => true
                    };
                });
                ShowToast($"🔍 '{clicked.DisplayName}' Filter Applied");
            }
        }

        // --- Color Similarity Check ---
        private bool IsColorSimilar(string hex1, string hex2, double threshold = 65.0)
        {
            try
            {
                Color c1 = (Color)ColorConverter.ConvertFromString(hex1);
                Color c2 = (Color)ColorConverter.ConvertFromString(hex2);
                return Math.Sqrt(Math.Pow(c1.R - c2.R, 2) + Math.Pow(c1.G - c2.G, 2) + Math.Pow(c1.B - c2.B, 2)) <= threshold;
            }
            catch { return false; }
        }

        private void NewLabelNameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddLabel_Click(sender, e);
        }

        private void AddLabel_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item && !string.IsNullOrWhiteSpace(NewLabelNameBox.Text))
            {
                string hexColor = InspectorColorPickerBtn.Content?.ToString() ?? "#7FB3D5";
                if (!hexColor.StartsWith("#")) hexColor = "#7FB3D5";
                string tagName = NewLabelNameBox.Text.Trim();

                item.Labels.Add(new LabelTag { Name = tagName, HexColor = hexColor });
                if (!MasterTagCatalog.Any(t => t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase)))
                    MasterTagCatalog.Add(new LabelTag { Name = tagName, HexColor = hexColor });

                NewLabelNameBox.Clear();
                SaveCustomTagsCatalog();
                SaveAllData();
                ShowToast("🏷️ Tag Added");
            }
        }

        private void MoreToolsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MoreToolsBtn.ContextMenu != null)
            {
                MoreToolsBtn.ContextMenu.PlacementTarget = MoreToolsBtn;
                MoreToolsBtn.ContextMenu.IsOpen = true;
            }
        }

        private void ToggleAppleMusicPanel_Click(object sender, RoutedEventArgs e)
        {
            AppleMusicSearchBorder.Visibility = AppleMusicSearchBorder.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        // --- Open Composer URL or Google Search ---
        private void OpenComposerUrl_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item)
            {
                if (!string.IsNullOrWhiteSpace(item.ComposerUrl) && item.ComposerUrl.StartsWith("http"))
                    Process.Start(new ProcessStartInfo(item.ComposerUrl) { UseShellExecute = true });
                else
                {
                    string targetName = !string.IsNullOrWhiteSpace(item.Composer) ? item.Composer : item.Creator;
                    string query = Uri.EscapeDataString($"{targetName} composer discography VGMdb");
                    Process.Start(new ProcessStartInfo($"https://www.google.com/search?q={query}") { UseShellExecute = true });
                }
            }
        }

        // --- Custom Emoji Image Handling ---
        private void AddCustomEmojiImage_Click(object sender, RoutedEventArgs e)
        {
            if (UserFolderTree.SelectedItem is FolderNode targetFolder)
            {
                OpenFileDialog dlg = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.webp;*.bmp" };
                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        targetFolder.Icon = CreateSquare100Icon(dlg.FileName);
                        SaveFolders();
                        if (UserFolderTree.ContextMenu != null) UserFolderTree.ContextMenu.IsOpen = false;
                        ShowToast("✨ 100x100 Custom Folder Icon Applied!");
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        // --- Create Square 100x100 Icon from Image ---
        private string CreateSquare100Icon(string sourcePath)
        {
            BitmapImage original = new BitmapImage();
            original.BeginInit();
            original.UriSource = new Uri(sourcePath, UriKind.Absolute);
            original.CacheOption = BitmapCacheOption.OnLoad;
            original.EndInit();

            int minSide = Math.Min(original.PixelWidth, original.PixelHeight);
            CroppedBitmap squareCrop = new CroppedBitmap(original, new Int32Rect((original.PixelWidth - minSide) / 2, (original.PixelHeight - minSide) / 2, minSide, minSide));
            TransformedBitmap resized100 = new TransformedBitmap(squareCrop, new ScaleTransform(100.0 / minSide, 100.0 / minSide));

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(resized100));

            string destPath = Path.Combine(_iconFolderDir, $"icon_{Guid.NewGuid().ToString().Substring(0, 8)}.png");
            using (FileStream fs = new FileStream(destPath, FileMode.Create)) encoder.Save(fs);
            return destPath;
        }

        private void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is string selectedEmoji && UserFolderTree.SelectedItem is FolderNode targetFolder)
            {
                targetFolder.Icon = selectedEmoji;
                SaveFolders();
                if (UserFolderTree.ContextMenu != null) UserFolderTree.ContextMenu.IsOpen = false;
                ShowToast($"✨ Folder Icon Changed to '{selectedEmoji}'");
            }
        }
        private void ToggleViewMode_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModeBtn.Tag?.ToString() == "ImageBoard")
            {
                ViewModeBtn.Tag = "Curation";
                ViewModeBtn.Content = _currentLang switch
                {
                    "JA" => "🖼️ ボード表示",
                    "EN" => "🖼️ Moodboard",
                    _ => "🖼️ 이미지 보드"
                };
            }
            else
            {
                ViewModeBtn.Tag = "ImageBoard";
                ViewModeBtn.Content = _currentLang switch
                {
                    "JA" => "🎵 キュレーション",
                    "EN" => "🎵 Curation View",
                    _ => "🎵 큐레이션 뷰"
                };
            }
            GalleryList.Items.Refresh();
        }

        private void LoadFolders()
        {
            if (File.Exists(_folderFilePath))
            {
                try
                {
                    var folders = JsonSerializer.Deserialize<ObservableCollection<FolderNode>>(File.ReadAllText(_folderFilePath));
                    if (folders != null && folders.Count > 0)
                    {
                        foreach (var f in folders)
                            if (f.Name == "Music Reference") f.Name = "Curation";
                        FolderList = folders;
                        return;
                    }
                }
                catch { }
            }

            // If no folders exist, create default folders
            var musicFolder = new FolderNode { Name = "Curation", Icon = "🎵", IconColor = "#61AFEF" };
            musicFolder.SubFolders.Add(new FolderNode { Name = "Jazz / Hard Bop", Icon = "🎷", IconColor = "#E5C07B" });
            musicFolder.SubFolders.Add(new FolderNode { Name = "Game OST / SFX", Icon = "🎹", IconColor = "#C678DD" });

            var gameFolder = new FolderNode { Name = "Game Dev & Art", Icon = "🎮", IconColor = "#98C379" };
            gameFolder.SubFolders.Add(new FolderNode { Name = "UI / Fonts / Design", Icon = "🔤", IconColor = "#56B6C2" });
            gameFolder.SubFolders.Add(new FolderNode { Name = "3D Models / Shaders", Icon = "🧊", IconColor = "#E06C75" });

            FolderList.Add(new FolderNode { Name = "Default", Icon = "📁" });
            FolderList.Add(musicFolder);
            FolderList.Add(gameFolder);
            SaveFolders();
        }

        private void SaveFolders()
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            File.WriteAllText(_folderFilePath, JsonSerializer.Serialize(FolderList, options));
        }

        private IEnumerable<string> GetAllFolderNames(IEnumerable<FolderNode> nodes)
        {
            foreach (var node in nodes)
            {
                yield return node.Name;
                foreach (var sub in GetAllFolderNames(node.SubFolders)) yield return sub;
            }
        }

        private HashSet<string> GetTargetAndSubFolderNames(FolderNode root)
        {
            var set = new HashSet<string> { root.Name };
            foreach (var sub in GetAllFolderNames(root.SubFolders)) set.Add(sub);
            return set;
        }

        private void UserFolderTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (item != null) { item.IsSelected = true; item.Focus(); }
        }

        private void AddRootFolder_Click(object sender, RoutedEventArgs e)
        {
            string? name = PromptInput(
                Loc("新しい最上位フォルダの名前を入力してください:", "새 최상위 폴더 이름을 입력하세요:", "Enter a name for the new root folder:"),
                Loc("フォルダ追加", "폴더 추가", "Add Folder"),
                Loc("新規フォルダ", "새 폴더", "New Folder"));
            if (!string.IsNullOrWhiteSpace(name))
            {
                var node = new FolderNode { Name = name.Trim(), Icon = "📁" };
                if (_currentTheme == "White") { node.NameColor = "#1E1E22"; node.IconColor = "#555555"; }
                FolderList.Add(node);
                SaveFolders();
            }
        }

        private void AddSubFolder_Click(object sender, RoutedEventArgs e)
        {
            if (UserFolderTree.SelectedItem is FolderNode parentFolder)
            {
                string? name = PromptInput(
                    Loc($"'{parentFolder.Name}' のサブフォルダ名を入力してください:", $"'{parentFolder.Name}'의 하위 폴더 이름을 입력하세요:", $"Enter a subfolder name under '{parentFolder.Name}':"),
                    Loc("サブフォルダ追加", "하위 폴더 추가", "Add Subfolder"),
                    Loc("サブフォルダ", "하위 폴더", "Subfolder"));
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var node = new FolderNode { Name = name.Trim(), Icon = "📂" };
                    if (_currentTheme == "White") { node.NameColor = "#1E1E22"; node.IconColor = "#555555"; }
                    parentFolder.SubFolders.Add(node);
                    parentFolder.IsExpanded = true;
                    SaveFolders();
                }
            }
        }

        // --- Folder Rename & Delete Handling ---
        private void RenameFolder_Click(object sender, RoutedEventArgs e)
        {
            if (UserFolderTree.SelectedItem is FolderNode targetFolder)
            {
                if (targetFolder.IsLocked)
                {
                    ShowToast(Loc("🔒 ロックされたフォルダです。", "🔒 잠긴 폴더는 이름을 변경할 수 없습니다!", "🔒 Folder is locked!"));
                    return;
                }

                string? newName = PromptInput(
                    Loc("新しいフォルダ名を入力してください:", "새로운 폴더 이름을 입력하세요:", "Enter a new folder name:"),
                    Loc("フォルダ名変更", "폴더 이름 변경", "Rename Folder"),
                    targetFolder.Name);
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    string oldName = targetFolder.Name;
                    targetFolder.Name = newName.Trim();
                    foreach (var item in ReferenceList)
                    {
                        if (item.FolderName == oldName) item.FolderName = targetFolder.Name;
                        if (item.FolderNames.Contains(oldName))
                        {
                            item.FolderNames.Remove(oldName);
                            item.FolderNames.Add(targetFolder.Name);
                        }
                    }
                    SaveAllData();
                }
            }
        }

        private void DeleteFolder_Click(object sender, RoutedEventArgs e)
        {
            if (UserFolderTree.SelectedItem is FolderNode targetFolder)
            {
                if (targetFolder.IsLocked)
                {
                    ShowToast(Loc("🔒 ロックされたフォルダは削除できません。", "🔒 잠긴 폴더는 삭제할 수 없습니다!", "🔒 Locked folder cannot be deleted!"));
                    return;
                }

                if (MessageBox.Show(
                    Loc($"'{targetFolder.Name}' フォルダを削除しますか？", $"'{targetFolder.Name}' 폴더를 삭제할까요?", $"Delete folder '{targetFolder.Name}'?"),
                    Loc("フォルダ削除", "폴더 삭제", "Delete Folder"), MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    RemoveFolderRecursive(FolderList, targetFolder);
                    if (FolderList.Count == 0) FolderList.Add(new FolderNode { Name = Loc("基本フォルダ", "기본 폴더", "Default"), Icon = "📁" });
                    SaveAllData();
                    SmartFolderList.SelectedIndex = 0;
                }
            }
        }

        // Recursive removal of a folder node from the tree
        private bool RemoveFolderRecursive(ObservableCollection<FolderNode> list, FolderNode target)
        {
            if (list.Remove(target)) return true;
            foreach (var node in list)
                if (RemoveFolderRecursive(node.SubFolders, target)) return true;
            return false;
        }

        // --- Localization Helper ---
        private string Loc(string ja, string ko, string en) => _currentLang switch { "JA" => ja, "EN" => en, _ => ko };
        // --- Prompt Input Dialog ---
        private string? PromptInput(string message, string title, string defaultValue = "")
        {
            Window prompt = new Window
            {
                Width = 380,
                Height = 180,
                Title = title,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
                Foreground = Brushes.White,
                ResizeMode = ResizeMode.NoResize
            };
            StackPanel panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = message, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            TextBox inputBox = new TextBox { Text = defaultValue, Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)), Foreground = Brushes.White, Padding = new Thickness(6), BorderThickness = new Thickness(1) };
            panel.Children.Add(inputBox);

            Button okBtn = new Button
            {
                Content = Loc("確認 (OK)", "확인", "OK"),
                Width = 90,
                Height = 30,
                Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                IsDefault = true
            };
            okBtn.Click += (s, e) => { prompt.DialogResult = true; prompt.Close(); };
            panel.Children.Add(okBtn);
            prompt.Content = panel;
            inputBox.Focus(); inputBox.SelectAll();
            return prompt.ShowDialog() == true ? inputBox.Text : null;
        }

        // [Optimization] Extract resolution without locking the file + 64px lightweight decoding and background asynchronous color extraction
        private void AnalyzeImageFeatures(ReferenceItem item)
        {
            if (string.IsNullOrEmpty(item.CoverImagePath) || !File.Exists(item.CoverImagePath)) return;
            string path = item.CoverImagePath;

            Task.Run(() =>
            {
                try
                {
                    // 1. Extract resolution without locking the file
                    string resText = "";
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        var frame = BitmapFrame.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                        resText = $"{frame.PixelWidth} x {frame.PixelHeight}";
                    }

                    // 2. DecodePixelWidth to 64px for fast color palette extraction & 32-bit BGRA32 format for pixel access
                    var smallBmp = new BitmapImage();
                    smallBmp.BeginInit();
                    smallBmp.UriSource = new Uri(path, UriKind.Absolute);
                    smallBmp.DecodePixelWidth = 64;
                    smallBmp.CacheOption = BitmapCacheOption.OnLoad;
                    smallBmp.EndInit();
                    smallBmp.Freeze();

                    var formatted = new FormatConvertedBitmap(smallBmp, PixelFormats.Bgra32, null, 0);
                    int w = formatted.PixelWidth, h = formatted.PixelHeight;
                    byte[] pixels = new byte[w * h * 4];
                    formatted.CopyPixels(pixels, w * 4, 0);

                    var colorBuckets = new Dictionary<string, (int Count, int R, int G, int B)>();
                    for (int i = 0; i < pixels.Length; i += 16)
                    {
                        byte b = pixels[i], g = pixels[i + 1], r = pixels[i + 2], a = pixels[i + 3];
                        if (a < 128) continue;

                        int qr = (r / 32) * 32, qg = (g / 32) * 32, qb = (b / 32) * 32;
                        string key = $"{qr},{qg},{qb}";
                        if (colorBuckets.TryGetValue(key, out var val))
                            colorBuckets[key] = (val.Count + 1, val.R + r, val.G + g, val.B + b);
                        else
                            colorBuckets[key] = (1, r, g, b);
                    }

                    var topColors = colorBuckets.Values
                        .OrderByDescending(v => v.Count)
                        .Take(5)
                        .Select(v => $"#{v.R / v.Count:X2}{v.G / v.Count:X2}{v.B / v.Count:X2}")
                        .ToList();

                    // 3. apply results to UI thread
                    Dispatcher.Invoke(() =>
                    {
                        item.Resolution = resText;
                        item.ColorPalette.Clear();
                        foreach (var hex in topColors) item.ColorPalette.Add(hex);
                    });
                }
                catch { }
            });
        }

        private void ColorSwatch_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is string hexColor)
            {
                Clipboard.SetText(hexColor);
                ShowToast($"🎨 Color code '{hexColor}' copied to clipboard!");
            }
        }

        private void CopyImageToClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item && !string.IsNullOrEmpty(item.CoverImagePath) && File.Exists(item.CoverImagePath))
            {
                try { Clipboard.SetImage(new BitmapImage(new Uri(item.CoverImagePath, UriKind.Absolute))); ShowToast("📋 Original image copied to clipboard!"); }
                catch { }
            }
        }

        private void SmartFolderList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SmartFolderList.SelectedItem is string label)
            {
                SetFocusEditMode(false); SetLightboxMode(false); CloseSettings_Click(sender, e);
                ApplySmartFolderByLabel(label);
            }
        }

        // --- Apply Smart Folder Filter Based on Label ---
        private void ApplySmartFolderByLabel(string label)
        {
            ApplyFilter(item =>
            {
                if (label.Contains("🗑️")) return item.IsDeleted;
                if (item.IsDeleted) return false;
                if (label.Contains("📂")) return true;
                if (label.Contains("⭐")) return item.IsBookmarked || item.BelongsToFolder("Bookmarks");
                if (label.Contains("📥")) return (string.IsNullOrEmpty(item.FolderName) || item.FolderName == "Default") && item.FolderNames.Count <= 1;
                if (label.Contains("🏷️")) return item.Labels == null || item.Labels.Count == 0;
                if (label.Contains("🎵")) return item.ItemType == "Music";
                if (label.Contains("🔊")) return item.ItemType == "Audio";
                if (label.Contains("🌐")) return item.ItemType == "URL" || !string.IsNullOrEmpty(item.WebUrl);
                if (label.Contains("🖼️")) return item.ItemType == "Image";
                if (label.Contains("🎬")) return item.ItemType == "Video" || item.FileExtension.Equals("GIF", StringComparison.OrdinalIgnoreCase);
                if (label.Contains("🔤")) return item.ItemType == "Font";
                if (label.Contains("🧊")) return item.ItemType == "3D" || item.ItemType == "Design" || item.ItemType == "Tool";
                if (label.Contains("📄")) return item.ItemType == "Text" || item.ItemType == "Document";
                return true;
            });

            if (label.Contains("🎲") && ReferenceList.Count > 0)
            {
                var activeItems = ReferenceList.Where(i => !i.IsDeleted).ToList();
                if (activeItems.Count > 0) GalleryList.SelectedItem = activeItems[new Random().Next(activeItems.Count)];
            }
        }

        private void UserFolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (UserFolderTree.SelectedItem is FolderNode selectedFolder)
            {
                SetFocusEditMode(false); SetLightboxMode(false); CloseSettings_Click(sender, e);
                SmartFolderList.UnselectAll();
                var validNames = GetTargetAndSubFolderNames(selectedFolder);
                ApplyFilter(item => !item.IsDeleted && validNames.Any(vn => item.BelongsToFolder(vn)));
            }
        }

        private void ApplyFilter(Func<ReferenceItem, bool> predicate)
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(GalleryList.ItemsSource);
            if (view == null) return;
            view.Filter = obj => obj is ReferenceItem item && predicate(item);
        }

        /// --- Load Database from JSON File ---
        private void LoadDatabase()
        {
            if (File.Exists(_dbFilePath))
            {
                try
                {
                    var items = JsonSerializer.Deserialize<ObservableCollection<ReferenceItem>>(File.ReadAllText(_dbFilePath));
                    if (items != null)
                    {
                        foreach (var item in items) ReferenceList.Add(item);
                    }
                }
                catch { }
            }
        }

        private void SaveAllData()
        {
            if (_saveDebounceTimer == null)
            {
                _saveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                _saveDebounceTimer.Tick += async (s, e) =>
                {
                    _saveDebounceTimer.Stop();
                    await SaveAllDataAsync();
                };
            }
            _saveDebounceTimer.Stop();
            _saveDebounceTimer.Start();
        }

        private async Task SaveAllDataAsync()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string dbJson = JsonSerializer.Serialize(ReferenceList, options);
                string folderJson = JsonSerializer.Serialize(FolderList, options);

                await File.WriteAllTextAsync(_dbFilePath, dbJson);
                await File.WriteAllTextAsync(_folderFilePath, folderJson);
            }
            catch { }
        }

        // --- Immediate Save All Data (No Debounce) ---
        private void FlushSaveAllDataImmediate()
        {
            _saveDebounceTimer?.Stop();
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(_dbFilePath, JsonSerializer.Serialize(ReferenceList, options));
            }
            catch { }

            SaveFolders();
            SaveCustomTagsCatalog();
            SaveUserPreferences();
        }

        private void SaveDb_Click(object sender, RoutedEventArgs e)
        {
            SaveAllData();
            SaveCustomTagsCatalog();
            ShowToast("✨ Save Complete!");
        }

        private void GalleryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int selCount = GalleryList.SelectedItems.Count;
            MultiSelectCountText.Text = selCount > 1 ? $"({selCount} selected)" : "";

            if (GalleryList.SelectedItem is ReferenceItem selected)
            {
                DetailPanel.DataContext = selected;
                FocusEditView.DataContext = selected;
                LightboxView.DataContext = selected;
                SearchPopupList.Visibility = Visibility.Collapsed;
                LoadMediaForSelectedItem(selected);
            }
        }

        private void AddMenuBtn_Click(object sender, RoutedEventArgs e)
        {
            if (AddMenuBtn.ContextMenu != null)
            {
                AddMenuBtn.ContextMenu.PlacementTarget = AddMenuBtn;
                AddMenuBtn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                AddMenuBtn.ContextMenu.IsOpen = true;
            }
        }

        private void UploadFilesMenu_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Title = "Select Reference Files", Filter = "All Reference Files|*.*", Multiselect = true };
            if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
                ProcessDroppedOrUploadedFiles(dlg.FileNames);
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                ProcessDroppedOrUploadedFiles(files);
            }
        }

        // --- Process Dropped or Uploaded Files ---
        private void ProcessDroppedOrUploadedFiles(string[] files)
        {
            string targetFolder = UserFolderTree.SelectedItem is FolderNode fn ? fn.Name : "Default";

            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                FileInfo fi = new FileInfo(file);
                string fileSizeInfo = $"{fi.Length / 1024.0:F1} KB ({ext.ToUpper().TrimStart('.')})";

                if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp" || ext == ".bmp" || ext == ".gif")
                {
                    string copiedPath = CopyImageToLocalFolder(file);
                    var newItem = new ReferenceItem { ItemType = ext == ".gif" ? "Video" : "Image", Title = Path.GetFileNameWithoutExtension(file), Creator = ext == ".gif" ? "GIF Animation" : "Image / Sprite", CoverImagePath = copiedPath, MediaFilePath = copiedPath, FolderName = targetFolder };
                    AnalyzeImageFeatures(newItem);
                    ReferenceList.Insert(0, newItem); GalleryList.SelectedItem = newItem;
                }
                else if (ext == ".mp3" || ext == ".wav" || ext == ".ogg" || ext == ".flac" || ext == ".flp" || ext == ".mp4" || ext == ".webm" || ext == ".avi" || ext == ".mov")
                {
                    string destMedia = CopyFileToMediaFolder(file);
                    bool isVideo = ext == ".mp4" || ext == ".webm" || ext == ".avi" || ext == ".mov";
                    string badgeCover = GenerateFileTypePlaceholderCover(ext.TrimStart('.'), isVideo ? "VIDEO REFERENCE" : "AUDIO / SFX", fileSizeInfo, isVideo ? Color.FromRgb(220, 80, 80) : Color.FromRgb(86, 156, 214));
                    var newItem = new ReferenceItem { ItemType = isVideo ? "Video" : "Audio", Title = Path.GetFileNameWithoutExtension(file), Creator = isVideo ? "Movie Clip" : "Audio / SFX", CoverImagePath = badgeCover, MediaFilePath = destMedia, Resolution = fileSizeInfo, FolderName = targetFolder };
                    AnalyzeImageFeatures(newItem);
                    ReferenceList.Insert(0, newItem); GalleryList.SelectedItem = newItem;
                    if (isVideo) _ = ExtractVideoThumbnailAsync(newItem, destMedia);
                }
                else if (ext == ".ttf" || ext == ".otf")
                {
                    string destFont = CopyFileToMediaFolder(file);
                    string fontFamilyName = Path.GetFileNameWithoutExtension(file);
                    try { GlyphTypeface glyph = new GlyphTypeface(new Uri(destFont, UriKind.Absolute)); fontFamilyName = glyph.FamilyNames.Values.FirstOrDefault() ?? fontFamilyName; } catch { }
                    string badgeCover = GenerateFileTypePlaceholderCover(ext.TrimStart('.'), fontFamilyName, "Font Asset", Color.FromRgb(186, 255, 201));
                    var newItem = new ReferenceItem { ItemType = "Font", Title = fontFamilyName, Creator = "Typography / Font", CoverImagePath = badgeCover, MediaFilePath = destFont, Resolution = fileSizeInfo, FolderName = targetFolder };
                    ReferenceList.Insert(0, newItem); GalleryList.SelectedItem = newItem;
                }
                else if (ext == ".fbx" || ext == ".blend" || ext == ".obj" || ext == ".dae" || ext == ".max")
                {
                    string dest3D = CopyFileToMediaFolder(file);
                    string badgeCover = GenerateFileTypePlaceholderCover(ext.TrimStart('.'), "3D MODEL ASSET", fileSizeInfo, Color.FromRgb(255, 179, 186));
                    var newItem = new ReferenceItem { ItemType = "3D", Title = Path.GetFileName(file), Creator = "3D Mesh / Blender", CoverImagePath = badgeCover, MediaFilePath = dest3D, Resolution = fileSizeInfo, FolderName = targetFolder };
                    AnalyzeImageFeatures(newItem);
                    ReferenceList.Insert(0, newItem); GalleryList.SelectedItem = newItem;
                }
                else if (ext == ".psd" || ext == ".ai" || ext == ".fig" || ext == ".sketch" || ext == ".svg" || ext == ".xd")
                {
                    string destDesign = CopyFileToMediaFolder(file);
                    string badgeCover = GenerateFileTypePlaceholderCover(ext.TrimStart('.'), "DESIGN SOURCE", fileSizeInfo, Color.FromRgb(224, 187, 228));
                    var newItem = new ReferenceItem { ItemType = "Design", Title = Path.GetFileName(file), Creator = "Adobe / Figma / Design", CoverImagePath = badgeCover, MediaFilePath = destDesign, Resolution = fileSizeInfo, FolderName = targetFolder };
                    foreach (var f in ExtractFontsFromDocument(destDesign)) newItem.DetectedFonts.Add(f);
                    AnalyzeImageFeatures(newItem);
                    ReferenceList.Insert(0, newItem); GalleryList.SelectedItem = newItem;
                }
                else if (ext == ".pdf" || ext == ".docx" || ext == ".pptx" || ext == ".xlsx")
                {
                    string destDoc = CopyFileToMediaFolder(file);
                    string badgeCover = GenerateFileTypePlaceholderCover(ext.TrimStart('.'), "DOCUMENT FILE", fileSizeInfo, Color.FromRgb(255, 223, 186));
                    var newItem = new ReferenceItem { ItemType = "Document", Title = Path.GetFileName(file), Creator = "Document Reference", CoverImagePath = badgeCover, MediaFilePath = destDoc, Resolution = fileSizeInfo, FolderName = targetFolder };
                    foreach (var f in ExtractFontsFromDocument(destDoc)) newItem.DetectedFonts.Add(f);
                    AnalyzeImageFeatures(newItem);
                    ReferenceList.Insert(0, newItem); GalleryList.SelectedItem = newItem;
                }
                else if (ext == ".txt" || ext == ".md" || ext == ".xml" || ext == ".json" || ext == ".cs" || ext == ".shader" || ext == ".hlsl" || ext == ".cpp")
                {
                    string destTxt = CopyFileToMediaFolder(file);
                    var newItem = new ReferenceItem { ItemType = "Text", Title = Path.GetFileName(file), Creator = $"Code / Markdown ({ext})", TextContent = File.ReadAllText(file), MediaFilePath = destTxt, Resolution = fileSizeInfo, FolderName = targetFolder };
                    ReferenceList.Insert(0, newItem); GalleryList.SelectedItem = newItem;
                }
                else
                {
                    string badgeCover = GenerateFileTypePlaceholderCover(ext.TrimStart('.'), "UTILITY / TOOL", fileSizeInfo, Color.FromRgb(154, 220, 255));
                    var newItem = new ReferenceItem { ItemType = "Tool", Title = Path.GetFileName(file), Creator = "External Tool / Binary", CoverImagePath = badgeCover, MediaFilePath = file, Resolution = fileSizeInfo, FolderName = targetFolder };
                    AnalyzeImageFeatures(newItem);
                    ReferenceList.Insert(0, newItem); GalleryList.SelectedItem = newItem;
                }
            }
            SaveAllData();
            ShowToast($"📥 {files.Length} reference(s) added.");
        }

        private string CopyImageToLocalFolder(string sourceFilePath)
        {
            string destPath = Path.Combine(_imageFolderDir, $"{Guid.NewGuid().ToString().Substring(0, 8)}_{Path.GetFileName(sourceFilePath)}");
            File.Copy(sourceFilePath, destPath, true);
            return destPath;
        }

        private string CopyFileToMediaFolder(string sourceFilePath)
        {
            string destPath = Path.Combine(_mediaFolderDir, $"{Guid.NewGuid().ToString().Substring(0, 8)}_{Path.GetFileName(sourceFilePath)}");
            File.Copy(sourceFilePath, destPath, true);
            return destPath;
        }

        private void ChangeImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item)
            {
                OpenFileDialog dlg = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.webp;*.bmp" };
                if (dlg.ShowDialog() == true)
                {
                    item.CoverImagePath = CopyImageToLocalFolder(dlg.FileName);
                    AnalyzeImageFeatures(item);
                    SaveAllData(); GalleryList.Items.Refresh();
                    ShowToast("🖼️ Cover Image Changed");
                }
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string targetFolder = UserFolderTree.SelectedItem is FolderNode fn ? fn.Name : "Default";
            var newItem = new ReferenceItem { ItemType = "Music", Title = "New Reference", Composer = "Composer", Creator = "Creator", FolderName = targetFolder };
            ReferenceList.Insert(0, newItem); GalleryList.SelectedItem = newItem;
            SaveAllData();
            ShowToast("✨ New Reference Card Added");
        }

        private void AddTextNote_Click(object sender, RoutedEventArgs e)
        {
            string targetFolder = UserFolderTree.SelectedItem is FolderNode fn ? fn.Name : "Default";
            var newItem = new ReferenceItem
            {
                ItemType = "Text",
                Title = "New Markdown Note.md",
                Creator = "Markdown Note",
                FolderName = targetFolder,
                TextContent = "# Title\n## title\n- list 1\n- list 2\n> notes."
            };
            ReferenceList.Insert(0, newItem); GalleryList.SelectedItem = newItem;
            SaveAllData();
            SetFocusEditMode(true);
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item)
            {
                bool isShiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                ExecuteDeleteItem(item, isShiftPressed);
            }
        }

        private void ExecuteDeleteItem(ReferenceItem item, bool immediatePermanent)
        {
            var targets = GalleryList.SelectedItems.Cast<ReferenceItem>().ToList();
            if (!targets.Contains(item)) targets = new List<ReferenceItem> { item };

            if (item.IsLocked)
            {
                ShowToast(Loc("🔒 ロックされたファイルは削除できません。", "🔒 잠금 설정된 파일은 삭제할 수 없습니다!", "🔒 Locked file cannot be deleted!"));
                return;
            }

            if (immediatePermanent || item.IsDeleted)
            {
                if (!immediatePermanent && MessageBox.Show($"Delete {targets.Count} items permanently?", "Delete Permanently", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;

                foreach (var t in targets) ReferenceList.Remove(t);
                SetFocusEditMode(false); SetLightboxMode(false);
                GalleryList.SelectedItem = ReferenceList.FirstOrDefault(i => !i.IsDeleted);
                SaveAllData();
                ShowToast($"🗑️ {targets.Count} Deleted ");
            }
            else
            {
                if (MessageBox.Show($"Move {targets.Count} items to the trash?", "Move to Trash", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    foreach (var t in targets) t.IsDeleted = true;
                    SetFocusEditMode(false); SetLightboxMode(false);
                    CollectionViewSource.GetDefaultView(GalleryList.ItemsSource)?.Refresh();
                    SaveAllData();
                    ShowToast($"🗑️ {targets.Count} moved to Trash ");
                }
            }
        }

        private void RemoveLabel_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is LabelTag tag && DetailPanel.DataContext is ReferenceItem item)
            {
                if (item.IsLocked)
                {
                    ShowToast(Loc("🔒 ロックされたファイルは変更できません。", "🔒 잠긴 파일은 태그를 삭제할 수 없습니다.", "🔒 Locked item cannot be modified."));
                    return;
                }
                _removedTagHistory.Push(tag); // Ctrl+Z 
                item.Labels.Remove(tag);
                SaveAllData();
                ShowToast(Loc($"🏷️ '{tag.Name}' 削除 (Ctrl+Zで復元)", $"🏷️ '{tag.Name}' 삭제됨 (Ctrl+Z로 복원)", $"🏷️ Removed '{tag.Name}' (Ctrl+Z to Undo)"));
            }
        }

        // --- Auto Close for Search Popup ---
        private void SearchBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _hasMouseEnteredSearchPopup = false;
            SearchFilterPopup.IsOpen = true;
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _hasMouseEnteredSearchPopup = false;
            SearchFilterPopup.IsOpen = true;
        }

        private async void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            await Task.Delay(150);
            if (!SearchBox.IsKeyboardFocusWithin && !SearchFilterPopup.IsKeyboardFocusWithin && !SearchFilterPopup.IsMouseOver)
                SearchFilterPopup.IsOpen = false;
        }

        private void SearchFilterPopup_MouseEnter(object sender, MouseEventArgs e) => _hasMouseEnteredSearchPopup = true;

        private void SearchFilterPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_hasMouseEnteredSearchPopup)
            {
                _hasMouseEnteredSearchPopup = false;
                SearchFilterPopup.IsOpen = false;
            }
        }

        private void CloseSearchAssist_Click(object sender, RoutedEventArgs e) => SearchFilterPopup.IsOpen = false;

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ExecuteAdvancedSearch();

        private void SearchFilterCheck_Changed(object sender, RoutedEventArgs e) { if (IsLoaded) ExecuteAdvancedSearch(); }

        // --- Advanced Search Logic ---
        private void ExecuteAdvancedSearch()
        {
            if (GalleryList?.ItemsSource == null) return;
            string keyword = SearchBox.Text.Trim().ToLower();
            ICollectionView view = CollectionViewSource.GetDefaultView(GalleryList.ItemsSource);
            if (view == null) return;

            bool onlyCurrentFolder = ChkSearchCurrentFolder?.IsChecked == true;
            bool onlyDocText = ChkSearchDocTextOnly?.IsChecked == true;
            bool checkTitleCreator = ChkSearchTitleCreator?.IsChecked != false;
            bool checkTags = ChkSearchTags?.IsChecked != false;
            bool checkFileType = ChkSearchFileType?.IsChecked != false;
            bool checkDate = ChkSearchDate?.IsChecked != false;

            HashSet<string>? allowedFolders = null;
            if (onlyCurrentFolder && UserFolderTree.SelectedItem is FolderNode selectedFolder)
                allowedFolders = GetTargetAndSubFolderNames(selectedFolder);

            if (string.IsNullOrEmpty(keyword) && !onlyDocText && !onlyCurrentFolder)
            {
                if (SmartFolderList.SelectedItem is string lbl) ApplySmartFolderByLabel(lbl);
                else ApplyFilter(i => !i.IsDeleted);
                return;
            }

            view.Filter = obj =>
            {
                if (obj is not ReferenceItem item || item.IsDeleted) return false;
                if (allowedFolders != null && !allowedFolders.Contains(item.FolderName)) return false;

                if (onlyDocText)
                {
                    if (string.IsNullOrEmpty(keyword)) return item.ItemType == "Text" || !string.IsNullOrEmpty(item.TextContent);
                    return item.TextContent?.ToLower().Contains(keyword) ?? false;
                }

                if (string.IsNullOrEmpty(keyword)) return true;

                bool matched = false;
                if (checkTitleCreator)
                {
                    matched |= (item.Title?.ToLower().Contains(keyword) ?? false) ||
                               (item.Composer?.ToLower().Contains(keyword) ?? false) ||
                               (item.Creator?.ToLower().Contains(keyword) ?? false) ||
                               (item.GameTitle?.ToLower().Contains(keyword) ?? false) ||
                               (item.Company?.ToLower().Contains(keyword) ?? false) ||
                               (item.Description?.ToLower().Contains(keyword) ?? false) ||
                               (item.TextContent?.ToLower().Contains(keyword) ?? false);
                }
                if (checkTags)
                    matched |= item.Labels.Any(l => l.Name.ToLower().Contains(keyword)) || item.DetectedFonts.Any(f => f.ToLower().Contains(keyword));
                if (checkFileType)
                    matched |= (item.ItemType?.ToLower().Contains(keyword) ?? false) || item.FileExtension.ToLower().Contains(keyword);
                if (checkDate)
                    matched |= item.CreatedAt.ToString("yyyy-MM-dd HH:mm").Contains(keyword);

                return matched;
            };
        }

        // --- Apple Music API ---
        private async void FetchCandidates_Click(object sender, RoutedEventArgs e)
        {
            string artist = ApiArtistBox.Text.Trim(), track = ApiTrackBox.Text.Trim();
            if (string.IsNullOrEmpty(artist) && string.IsNullOrEmpty(track)) return;

            try
            {
                ApiFetchButton.Content = "Searching..."; ApiFetchButton.IsEnabled = false;
                string searchTerm = $"{artist} {track}".Trim();
                string url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(searchTerm)}&entity=song&limit=15";
                string jsonResponse = await _httpClient.GetStringAsync(url);

                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                JsonElement root = doc.RootElement;
                if (root.GetProperty("resultCount").GetInt32() > 0)
                {
                    var candidateList = new List<MusicCandidate>();
                    foreach (JsonElement res in root.GetProperty("results").EnumerateArray())
                    {
                        string tName = res.TryGetProperty("trackName", out var t) ? (t.GetString() ?? "") : "";
                        string aName = res.TryGetProperty("artistName", out var a) ? (a.GetString() ?? "") : "";
                        string cName = res.TryGetProperty("collectionName", out var c) ? (c.GetString() ?? "") : "";
                        candidateList.Add(new MusicCandidate { DisplayTitle = tName, DisplaySub = $"{aName} — {cName}", RawData = res.Clone() });
                    }
                    SearchPopupList.ItemsSource = candidateList;
                    SearchPopupList.Visibility = Visibility.Visible;
                }
            }
            catch { }
            finally { ApiFetchButton.Content = "🔍 Apple Music Candidate Search"; ApiFetchButton.IsEnabled = true; }
        }

        private async void SearchPopupList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchPopupList.SelectedItem is MusicCandidate selected)
            {
                if (DetailPanel.DataContext is not ReferenceItem item) { AddButton_Click(sender, e); item = (ReferenceItem)DetailPanel.DataContext; }
                JsonElement target = selected.RawData;
                if (target.TryGetProperty("trackName", out var tElem)) item.Title = tElem.GetString() ?? item.Title;
                if (target.TryGetProperty("artistName", out var aElem))
                {
                    item.Creator = aElem.GetString() ?? item.Creator;
                    if (string.IsNullOrEmpty(item.Composer)) item.Composer = item.Creator;
                }

                string albumName = target.TryGetProperty("collectionName", out var colElem) ? (colElem.GetString() ?? "") : "";
                string releaseYear = target.TryGetProperty("releaseDate", out var dateElem) ? (dateElem.GetString() ?? "").Substring(0, Math.Min(4, (dateElem.GetString() ?? "").Length)) : "";
                item.GameTitle = string.IsNullOrEmpty(releaseYear) ? albumName : $"{albumName} ({releaseYear})";

                if (target.TryGetProperty("previewUrl", out var prevElem))
                {
                    string? previewAudioUrl = prevElem.GetString();
                    if (!string.IsNullOrEmpty(previewAudioUrl))
                    {
                        SharedMediaPlayer.Source = new Uri(previewAudioUrl, UriKind.Absolute);
                        WaveformImage.Source = GenerateWaveformBitmap("", item.Title);
                    }
                }

                if (target.TryGetProperty("artworkUrl100", out var artElem))
                {
                    string? lowResUrl = artElem.GetString();
                    if (!string.IsNullOrEmpty(lowResUrl))
                    {
                        item.CoverImagePath = await DownloadImageToLocalAsync(lowResUrl.Replace("100x100bb", "600x600bb"));
                        AnalyzeImageFeatures(item);
                    }
                }
                SaveAllData();
                SearchPopupList.Visibility = Visibility.Collapsed;
                ShowToast("🎵 Apple Music Jacket & Preview Link Connected");
            }
        }

        private async Task<string> DownloadImageToLocalAsync(string imageUrl)
        {
            byte[] imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
            string destPath = Path.Combine(_imageFolderDir, $"cover_{Guid.NewGuid().ToString().Substring(0, 8)}.jpg");
            await File.WriteAllBytesAsync(destPath, imageBytes);
            return destPath;
        }

        private void ApiTrackBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) FetchCandidates_Click(sender, e);
        }

        private void SearchWeb_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item)
            {
                string query = Uri.EscapeDataString($"{item.Composer} {item.Creator} {item.Title} {item.GameTitle}");
                Process.Start(new ProcessStartInfo($"https://www.google.com/search?q={query}") { UseShellExecute = true });
            }
        }

        private void SearchYouTube_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item)
            {
                string query = Uri.EscapeDataString($"{item.Composer} {item.Creator} {item.Title}");
                Process.Start(new ProcessStartInfo($"https://www.youtube.com/results?search_query={query}") { UseShellExecute = true });
            }
        }

        private void CopySnsTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item)
            {
                string tags = string.Join(" ", item.Labels.Select(l => $"#{l.Name.Replace(" ", "")}"));
                string template = $"🎵 [Reference Curator]\n• Title: {item.Title}\n• Composer: {item.Composer}\n• Artist: {item.Creator}\n• Work: {item.GameTitle} ({item.Company})\n\n💬 Note:\n{item.Description}\n\nTags: {tags}";
                Clipboard.SetText(template);
                ShowToast("📋 Copy Complete!");
            }
        }

        // --- Export SNS Card Image ---
        private void ExportSnsImage_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item)
            {
                try
                {
                    int renderWidth = 1080, renderHeight = 1080;
                    DrawingVisual drawingVisual = new DrawingVisual();
                    using (DrawingContext dc = drawingVisual.RenderOpen())
                    {
                        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 30, 30)), null, new Rect(0, 0, renderWidth, renderHeight));
                        if (!string.IsNullOrEmpty(item.CoverImagePath) && File.Exists(item.CoverImagePath))
                        {
                            var bitmapImage = new BitmapImage();
                            bitmapImage.BeginInit(); bitmapImage.UriSource = new Uri(item.CoverImagePath, UriKind.Absolute); bitmapImage.CacheOption = BitmapCacheOption.OnLoad; bitmapImage.EndInit();
                            double boxW = 920, boxH = 520, w = boxW, h = boxH;
                            double origRatio = bitmapImage.PixelWidth / (double)bitmapImage.PixelHeight;
                            if (origRatio > boxW / boxH) h = boxW / origRatio; else w = boxH * origRatio;
                            dc.DrawImage(bitmapImage, new Rect(80 + (boxW - w) / 2.0, 100 + (boxH - h) / 2.0, w, h));
                        }
                        dc.DrawText(new FormattedText(item.Title, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 48, Brushes.White, 96), new Point(80, 680));
                        dc.DrawText(new FormattedText($"{item.Composer} / {item.Creator}", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 30, Brushes.LightSkyBlue, 96), new Point(80, 750));
                        dc.DrawText(new FormattedText($"{item.GameTitle} ({item.Company})", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Arial"), 24, Brushes.DarkGray, 96), new Point(80, 805));
                    }
                    RenderTargetBitmap bmp = new RenderTargetBitmap(renderWidth, renderHeight, 96, 96, PixelFormats.Pbgra32);
                    bmp.Render(drawingVisual);
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bmp));
                    string filePath = Path.Combine(_workspaceRootDir, $"sns_card_{Guid.NewGuid().ToString().Substring(0, 6)}.png");
                    using (FileStream fs = new FileStream(filePath, FileMode.Create)) encoder.Save(fs);
                    ShowToast("📸 SNS Card Rendered");
                    Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"/select,\"{filePath}\"" });
                }
                catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
            }
        }

        private void ExportTool_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is ReferenceItem item)
            {
                string json = JsonSerializer.Serialize(item, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                File.WriteAllText(Path.Combine(_workspaceRootDir, "exported_reference.json"), json);
                ShowToast("📤 Export Complete!");
            }
        }

        private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) yield return typedChild;
                foreach (var sub in FindVisualChildren<T>(child)) yield return sub;
            }
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T typed) return typed;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }

    /// --- DependencyObject Extensions ---
    internal static class DependencyObjectExtensions
    {
        public static T? As<T>(this object? obj) where T : class => obj as T;
    }
}