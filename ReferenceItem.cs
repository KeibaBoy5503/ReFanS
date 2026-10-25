using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReFanS
{
    public class LabelTag : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _hexColor = "#FFB3BA";

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }
        public string HexColor
        {
            get => _hexColor;
            set { _hexColor = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class FilterTreeNode : INotifyPropertyChanged
    {
        private string _displayName = string.Empty;
        private string _hexColor = "#FFB3BA";
        private string _iconColor = "#E0E0E0";
        private string _nameColor = "#FFFFFF";
        private int _itemCount = 0;
        private bool _isExpanded = false;

        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }
        public string FullMatchKey { get; set; } = string.Empty;
        public string CategoryType { get; set; } = "Tag";
        public string HexColor
        {
            get => _hexColor;
            set { _hexColor = value; OnPropertyChanged(); }
        }
        public string IconColor
        {
            get => _iconColor;
            set { _iconColor = value; OnPropertyChanged(); }
        }
        public string NameColor
        {
            get => _nameColor;
            set { _nameColor = value; OnPropertyChanged(); }
        }
        public double HueSortKey { get; set; } = 0;
        public int ItemCount
        {
            get => _itemCount;
            set { _itemCount = value; OnPropertyChanged(); }
        }
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }
        public bool IsCategoryHeader { get; set; } = false;

        public ObservableCollection<FilterTreeNode> SubNodes { get; set; } = new ObservableCollection<FilterTreeNode>();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class FolderNode : INotifyPropertyChanged
    {
        private string _name = "New Folder";
        private string _icon = "📁";
        private string _iconColor = "#E0E0E0";
        private string _nameColor = "#FFFFFF";
        private bool _isExpanded = true;
        private bool _isLocked = false;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }
        public string Icon
        {
            get => _icon;
            set
            {
                _icon = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCustomImageIcon));
            }
        }
        public string IconColor
        {
            get => _iconColor;
            set { _iconColor = value; OnPropertyChanged(); }
        }
        public string NameColor
        {
            get => _nameColor;
            set { _nameColor = value; OnPropertyChanged(); }
        }
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }
        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                _isLocked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LockBadge));
            }
        }

        [JsonIgnore]
        public string LockBadge => _isLocked ? " 🔒" : "";

        [JsonIgnore]
        public bool IsCustomImageIcon => !string.IsNullOrEmpty(_icon) && (_icon.Contains('\\') || _icon.Contains('/')) && File.Exists(_icon);

        public ObservableCollection<FolderNode> SubFolders { get; set; } = new ObservableCollection<FolderNode>();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class ReferenceItem : INotifyPropertyChanged
    {
        private string _itemType = "Music";
        private string _title = string.Empty;
        private string _composer = string.Empty;
        private string _creator = string.Empty;
        private string _gameTitle = string.Empty;
        private string _company = string.Empty;
        private string _composerUrl = string.Empty;
        private string _webUrl = string.Empty;
        private string _description = string.Empty;
        private string _coverImagePath = string.Empty;
        private string _mediaFilePath = string.Empty;
        private string _folderName = "New Folder";
        private string _textContent = string.Empty;
        private string _resolution = string.Empty;
        private string _fontSampleText = " あらゆる現実をすべて自分のほうへねじ曲げたのだ。/ 다람쥐 헌 쳇바퀴에 타고파 / The quick brown fox 1234567890";
        private bool _isDeleted = false;
        private bool _isBookmarked = false;
        private bool _isLocked = false;
        private DateTime _createdAt = DateTime.Now;


        public string Id { get; set; } = Guid.NewGuid().ToString();

        public DateTime CreatedAt
        {
            get => _createdAt;
            set { _createdAt = value; OnPropertyChanged(); }
        }
        public string ItemType
        {
            get => _itemType;
            set
            {
                _itemType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TypeBadgeText));
                OnPropertyChanged(nameof(IsPlayableMedia));
            }
        }
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }
        public string Composer
        {
            get => _composer;
            set { _composer = value; OnPropertyChanged(); }
        }
        public string Creator
        {
            get => _creator;
            set { _creator = value; OnPropertyChanged(); }
        }
        public string GameTitle
        {
            get => _gameTitle;
            set { _gameTitle = value; OnPropertyChanged(); }
        }
        public string Company
        {
            get => _company;
            set { _company = value; OnPropertyChanged(); }
        }
        public string ComposerUrl
        {
            get => _composerUrl;
            set { _composerUrl = value; OnPropertyChanged(); }
        }
        public string WebUrl
        {
            get => _webUrl;
            set { _webUrl = value; OnPropertyChanged(); }
        }
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }
        public string CoverImagePath
        {
            get => _coverImagePath;
            set
            {
                _coverImagePath = value;
                _thumbnailSource = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ThumbnailSource));
            }
        }

        private ImageSource? _thumbnailSource;

        [JsonIgnore]
        public ImageSource? ThumbnailSource
        {
            get
            {
                if (_thumbnailSource != null) return _thumbnailSource;
                if (string.IsNullOrEmpty(_coverImagePath) || !File.Exists(_coverImagePath)) return null;

                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(_coverImagePath, UriKind.Absolute);
                    // optimize 360px width for thumbnail to reduce memory usage
                    bmp.DecodePixelWidth = 360;
                    // file locking issue workaround: load the image into memory and close the file stream
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bmp.EndInit();
                    // render the image to freeze it and make it cross-thread accessible
                    bmp.Freeze();
                    _thumbnailSource = bmp;
                }
                catch { return null; }

                return _thumbnailSource;
            }
        }
        public string MediaFilePath
        {
            get => _mediaFilePath;
            set
            {
                _mediaFilePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasMediaFile));
                OnPropertyChanged(nameof(FileExtension));
                OnPropertyChanged(nameof(TypeBadgeText));
                OnPropertyChanged(nameof(CustomFontFamily));
                OnPropertyChanged(nameof(IsPlayableMedia));
            }
        }

        // Multi-Classification Folder Support: FolderName is the primary folder, and FolderNames is a collection of all folders the item belongs to.
        public string FolderName
        {
            get => _folderName;
            set
            {
                _folderName = value;
                if (!string.IsNullOrWhiteSpace(value) && !FolderNames.Contains(value))
                {
                    if (FolderNames.Count == 0) FolderNames.Add(value);
                    else FolderNames[0] = value;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(MultiFolderDisplay));
            }
        }

        // multi-folder support: a collection of folder names the item belongs to, with the first one being the primary folder.
        public ObservableCollection<string> FolderNames { get; set; } = new ObservableCollection<string>();

        [JsonIgnore]
        public string MultiFolderDisplay => FolderNames.Count > 0 ? string.Join(", ", FolderNames) : _folderName;

        public bool BelongsToFolder(string targetFolder)
        {
            if (FolderNames.Contains(targetFolder)) return true;
            return string.Equals(_folderName, targetFolder, StringComparison.OrdinalIgnoreCase);
        }

        public void AddToFolder(string targetFolder)
        {
            if (!FolderNames.Contains(targetFolder))
                FolderNames.Add(targetFolder);
            if (string.IsNullOrEmpty(_folderName) || _folderName == "New Folder")
                _folderName = targetFolder;
            OnPropertyChanged(nameof(MultiFolderDisplay));
        }

        public string TextContent
        {
            get => _textContent;
            set { _textContent = value; OnPropertyChanged(); }
        }
        public string Resolution
        {
            get => _resolution;
            set { _resolution = value; OnPropertyChanged(); }
        }
        public string FontSampleText
        {
            get => _fontSampleText;
            set { _fontSampleText = value; OnPropertyChanged(); }
        }
        public bool IsDeleted
        {
            get => _isDeleted;
            set { _isDeleted = value; OnPropertyChanged(); }
        }
        public bool IsBookmarked
        {
            get => _isBookmarked;
            set
            {
                _isBookmarked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BookmarkIcon));
            }
        }
        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                _isLocked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LockIcon));
            }
        }

        [JsonIgnore]
        public string BookmarkIcon => _isBookmarked ? "⭐" : "☆";

        [JsonIgnore]
        public string LockIcon => _isLocked ? "🔒" : "🔓";

        [JsonIgnore]
        public bool HasMediaFile => !string.IsNullOrEmpty(_mediaFilePath) && File.Exists(_mediaFilePath);

        [JsonIgnore]
        public bool IsPlayableMedia => (_itemType == "Audio" || _itemType == "Video" || _itemType == "Music" || FileExtension.Equals("GIF", StringComparison.OrdinalIgnoreCase)) && HasMediaFile;
        [JsonIgnore]
        public string FileExtension => string.IsNullOrEmpty(_mediaFilePath) ? "" : Path.GetExtension(_mediaFilePath).ToUpper().TrimStart('.');

        // inline playback properties for audio/video items
        private bool _isInlinePlaying = false;
        private double _inlineProgress = 0;
        private string _inlineTimeText = "00:00";

        [JsonIgnore]
        public bool IsInlinePlaying
        {
            get => _isInlinePlaying;
            set
            {
                _isInlinePlaying = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(InlinePlayButtonIcon));
            }
        }

        [JsonIgnore]
        public string InlinePlayButtonIcon => _isInlinePlaying ? "⏸" : "▶";

        [JsonIgnore]
        public double InlineProgress
        {
            get => _inlineProgress;
            set { _inlineProgress = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
        public string InlineTimeText
        {
            get => _inlineTimeText;
            set { _inlineTimeText = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
        public string TypeBadgeText
        {
            get
            {
                string ext = FileExtension;
                return _itemType switch
                {
                    "Music" => "🎵 CURATION",
                    "URL" => "🌐 WEB URL",
                    "Image" => string.IsNullOrEmpty(ext) ? "🖼️ IMAGE" : $"🖼️ {ext}",
                    "Audio" => string.IsNullOrEmpty(ext) ? "🔊 AUDIO" : $"🔊 {ext}",
                    "Video" => string.IsNullOrEmpty(ext) ? "🎬 VIDEO" : $"🎬 {ext}",
                    "Font" => string.IsNullOrEmpty(ext) ? "🔤 FONT" : $"🔤 {ext}",
                    "3D" => string.IsNullOrEmpty(ext) ? "🧊 3D" : $"🧊 {ext}",
                    "Design" => string.IsNullOrEmpty(ext) ? "🎨 DESIGN" : $"🎨 {ext}",
                    "Document" => string.IsNullOrEmpty(ext) ? "📕 DOC" : $"📕 {ext}",
                    "Tool" => string.IsNullOrEmpty(ext) ? "🛠️ TOOL" : $"🛠️ {ext}",
                    "Text" => string.IsNullOrEmpty(ext) ? "📄 DOC" : $"📄 {ext}",
                    _ => _itemType.ToUpper()
                };
            }
        }

        [JsonIgnore]
        public FontFamily CustomFontFamily
        {
            get
            {
                if (_itemType == "Font" && !string.IsNullOrEmpty(_mediaFilePath) && File.Exists(_mediaFilePath))
                {
                    try
                    {
                        GlyphTypeface glyph = new GlyphTypeface(new Uri(_mediaFilePath, UriKind.Absolute));
                        string? familyName = glyph.FamilyNames.Values.FirstOrDefault();
                        if (!string.IsNullOrEmpty(familyName))
                        {
                            string dir = Path.GetDirectoryName(_mediaFilePath)!;
                            return new FontFamily(new Uri(dir + "\\", UriKind.Absolute), $"./#{familyName}");
                        }
                    }
                    catch { }
                }
                return new FontFamily("Malgun Gothic, Arial");
            }
        }

        public ObservableCollection<LabelTag> Labels { get; set; } = new ObservableCollection<LabelTag>();
        public ObservableCollection<string> ColorPalette { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> DetectedFonts { get; set; } = new ObservableCollection<string>();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}