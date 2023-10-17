using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using Avalonia.Platform.Storage;
using CodeGenerator.Events;
using CodeGenerator.Models;
using CodeGenerator.Utils;
using CodeGenerator.Views;
using MsBox.Avalonia;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Xceed.Document.NET;
using Xceed.Words.NET;

namespace CodeGenerator.ViewModels;

public class MainWindowViewModel : BindableBase
{
    #region VM

    private ObservableCollection<DirectoryStruct> _dirItemCollection = new();

    public ObservableCollection<DirectoryStruct> DirItemCollection
    {
        get => _dirItemCollection;
        set
        {
            _dirItemCollection = value;
            RaisePropertyChanged();
        }
    }

    private string _suffixType = string.Empty;

    public string SuffixType
    {
        get => _suffixType;
        set
        {
            _suffixType = value;
            RaisePropertyChanged();
        }
    }

    private ObservableCollection<string> _fileSuffixCollection = new();

    public ObservableCollection<string> FileSuffixCollection
    {
        get => _fileSuffixCollection;
        set
        {
            _fileSuffixCollection = value;
            RaisePropertyChanged();
        }
    }

    private ObservableCollection<string> _fileCollection = new();

    public ObservableCollection<string> FileCollection
    {
        get => _fileCollection;
        set
        {
            _fileCollection = value;
            RaisePropertyChanged();
        }
    }

    private int _handleTextProgress;

    public int HandleTextProgress
    {
        get => _handleTextProgress;
        set
        {
            _handleTextProgress = value;
            RaisePropertyChanged();
        }
    }

    private int _effectiveCodeLines;

    public int EffectiveCodeLines
    {
        get => _effectiveCodeLines;
        set
        {
            _effectiveCodeLines = value;
            RaisePropertyChanged();
        }
    }

    #endregion

    #region DelegateCommand

    public DelegateCommand<MainWindow> SelectDirCommand { set; get; }
    public DelegateCommand<MainWindow> DirItemSelectedCommand { set; get; }
    public DelegateCommand<MainWindow> MouseDoubleClickCommand { set; get; }
    public DelegateCommand AddFileSuffixTypeCommand { set; get; }
    public DelegateCommand GeneratorCodeCommand { set; get; }

    #endregion

    private DirectoryStruct _directory;
    private string _outputFilePath = string.Empty;

    /// <summary>
    /// 需要格式化的文件全路径集
    /// </summary>
    private readonly ObservableCollection<string> _generateFilePathCollection = new();

    /// <summary>
    /// 不做限制的文件全路径集
    /// </summary>
    private readonly ObservableCollection<string> _filePathCollection = new();

    private readonly BackgroundWorker _backgroundWorker;

    public MainWindowViewModel(IEventAggregator eventAggregator)
    {
        _backgroundWorker = new BackgroundWorker();
        _backgroundWorker.WorkerReportsProgress = true;
        _backgroundWorker.WorkerSupportsCancellation = true;
        _backgroundWorker.DoWork += Worker_OnDoWork;
        _backgroundWorker.ProgressChanged += Worker_OnProgressChanged;
        _backgroundWorker.RunWorkerCompleted += Worker_OnRunWorkerCompleted;

        eventAggregator.GetEvent<DirectoryEvent>().Subscribe(delegate(int i)
        {
            DirItemCollection.RemoveAt(i);
            FileCollection.Clear();
            _filePathCollection.Clear();
        });

        eventAggregator.GetEvent<FileNameTagEvent>().Subscribe(delegate(string s) { FileCollection.Remove(s); });

        eventAggregator.GetEvent<FileSuffixTagEvent>().Subscribe(delegate(string s)
        {
            FileSuffixCollection.Remove(s);
        });

        //打开文件夹选择器
        SelectDirCommand = new DelegateCommand<MainWindow>(OpenFolderPicker);

        //左侧列表选中事件
        DirItemSelectedCommand = new DelegateCommand<MainWindow>(delegate(MainWindow window)
        {
            var selectedItem = window.DirListBox.SelectedItem;
            if (selectedItem == null)
            {
                return;
            }

            _directory = (DirectoryStruct) selectedItem;
            TraverseDir();
        });

        //打开文件
        MouseDoubleClickCommand = new DelegateCommand<MainWindow>(OpenFile);
        AddFileSuffixTypeCommand = new DelegateCommand(AddFileSuffixType);
        GeneratorCodeCommand = new DelegateCommand(GeneratorCode);
    }

    private async void OpenFolderPicker(MainWindow window)
    {
        var temp = DirItemCollection.Select(file => file.FullPath).ToList();

        var folder = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
        if (folder.Any())
        {
            var dirPath = folder[0].Path.AbsolutePath;
            if (temp.Contains(dirPath))
            {
                await MessageBoxManager.GetMessageBoxStandard("错误", "文件夹已添加，请勿重复添加").ShowAsync();
                return;
            }

            var file = new FileInfo(dirPath);
            _directory = new DirectoryStruct
            {
                Name = file.Name,
                FullPath = dirPath
            };
            DirItemCollection.Add(_directory);

            //遍历文件夹
            TraverseDir();
        }
    }

    private async void OpenFile(MainWindow window)
    {
        var fileIndex = window.FileListBox.SelectedIndex;
        var file = new FileInfo(_filePathCollection[fileIndex]);
        if (RuntimeCache.ImageSuffixArray.Contains(file.Extension))
        {
            await new ShowImageWindow(file).ShowDialog(window);
        }
        else
        {
            if (RuntimeCache.TextSuffixArray.Contains(file.Extension))
            {
                await new ShowTextWindow(file).ShowDialog(window);
            }
            else
            {
                await MessageBoxManager.GetMessageBoxStandard("错误", "文件类型无法打开，请重新选择").ShowAsync();
            }
        }
    }

    private async void AddFileSuffixType()
    {
        if (string.IsNullOrWhiteSpace(_suffixType))
        {
            await MessageBoxManager.GetMessageBoxStandard("错误", "文件类型为空，无法添加").ShowAsync();
            return;
        }

        if (FileSuffixCollection.Contains(_suffixType) || FileSuffixCollection.Contains($".{_suffixType}"))
        {
            await MessageBoxManager.GetMessageBoxStandard("错误", "文件类型已添加，请勿重复添加").ShowAsync();
            return;
        }

        FileSuffixCollection.Add(_suffixType.Contains("\\.") ? _suffixType : $".{_suffixType}");

        //添加之后将输入框置空
        SuffixType = string.Empty;
    }

    private async void GeneratorCode()
    {
        if (!_fileSuffixCollection.Any())
        {
            await MessageBoxManager.GetMessageBoxStandard("错误", "请设置需要格式化的文件后缀").ShowAsync();
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var current = WindowsIdentity.GetCurrent();
            var currentName = current.Name;
            var userName = currentName.Split('\\')[1];

            _outputFilePath = $@"C:\Users\{userName}\Desktop\软著代码";
        }
        else
        {
            _outputFilePath = $"{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}/软著代码";
        }

        //按照设置的文件后缀遍历文件
        TraverseDir();

        //启动文件处理后台线程
        if (_backgroundWorker.IsBusy)
        {
            await MessageBoxManager.GetMessageBoxStandard("错误", "当前正在处理文件中").ShowAsync();
            return;
        }

        _backgroundWorker.RunWorkerAsync();
    }

    /// <summary>
    /// 遍历文件夹
    /// </summary>
    private void TraverseDir()
    {
        if (FileCollection.Any())
        {
            FileCollection.Clear();
        }

        if (_generateFilePathCollection.Any())
        {
            _generateFilePathCollection.Clear();
        }

        EffectiveCodeLines = 0;

        var files = _directory.FullPath.GetDirFiles();
        foreach (var file in files)
        {
            FileCollection.Add(file.Name);
            _filePathCollection.Add(file.FullName);
            if (_fileSuffixCollection.Contains(file.Extension))
            {
                _generateFilePathCollection.Add(file.FullName);
            }
        }
    }

    private void Worker_OnDoWork(object? sender, DoWorkEventArgs e)
    {
        //所有符合要求的代码文件内容
        var codeContentArray = new List<string>();
        var i = 0;
        foreach (var filePath in _generateFilePathCollection)
        {
            //读取源文件，跳过读取空白行
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    codeContentArray.Add(line);
                }
            }

            //更新处理进度
            i++;
            var percent = i / (float) _generateFilePathCollection.Count;
            _backgroundWorker.ReportProgress((int) (percent * 100));

            //此行代码根据情况可选择删除或者保留
            Thread.Sleep(20);
        }

        //生成带有缩进格式的Text，便于写入word
        File.WriteAllLines($"{_outputFilePath}.txt", codeContentArray);

        //设置有效代码行数
        EffectiveCodeLines = codeContentArray.Count;

        //读取整篇格式化好了的Text写入word
        var text = File.ReadAllText($"{_outputFilePath}.txt");
        var docX = DocX.Create(_outputFilePath);
        var paragraph = docX.InsertParagraph();
        var fmt = new Formatting
        {
            FontFamily = new Font("微软雅黑"),
            Size = 7 //软著要求每页50行代码，7号字体正好合适
        };
        paragraph.Append(text, fmt);
        try
        {
            docX.Save();
        }
        catch (ArgumentException)
        {
            MessageBoxManager.GetMessageBoxStandard("错误", "文件类型错误，无法生成代码文件").ShowAsync();
        }
    }

    private void Worker_OnProgressChanged(object? sender, ProgressChangedEventArgs e)
    {
        HandleTextProgress = e.ProgressPercentage;
    }

    private void Worker_OnRunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
    }
}