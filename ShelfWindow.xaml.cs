using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TangerineWin
{
    public partial class ShelfWindow : Window
    {
        private readonly List<string> _heldFiles = new();
        private Point _dragStartPoint;
        private bool _isDraggingOut = false;

        public ShelfWindow()
        {
            InitializeComponent();
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 20;
            Top = (area.Height - Height) / 2;
        }

        public void AddFiles(IEnumerable<string> files)
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var f in files)
                {
                    if ((File.Exists(f) || Directory.Exists(f)) && !_heldFiles.Contains(f))
                    {
                        _heldFiles.Add(f);
                    }
                }
                RefreshUI();
                if (!IsVisible) Show();
                Activate();
            });
        }

        private void RefreshUI()
        {
            FilesListBox.Items.Clear();
            foreach (var f in _heldFiles)
            {
                FilesListBox.Items.Add(new ListBoxItem
                {
                    Content = "📄 " + Path.GetFileName(f),
                    ToolTip = f,
                    Tag = f,
                    Padding = new Thickness(4, 3, 4, 3)
                });
            }
            ShelfCountText.Text = $"Hold Shelf ({_heldFiles.Count})";
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Shelf_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddFiles(dropped);
            }
        }

        private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void ListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDraggingOut)
            {
                Point pos = e.GetPosition(null);
                if (Math.Abs(pos.X - _dragStartPoint.X) > 6 || Math.Abs(pos.Y - _dragStartPoint.Y) > 6)
                {
                    var selected = FilesListBox.SelectedItems.Cast<ListBoxItem>()
                        .Select(i => i.Tag as string)
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToArray();

                    if (selected.Length == 0 && _heldFiles.Count > 0)
                        selected = _heldFiles.ToArray();

                    if (selected.Length > 0)
                        StartNativeDragOut(selected!);
                }
            }
        }

        private void DragAllBadge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void DragAllBadge_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDraggingOut && _heldFiles.Count > 0)
            {
                Point pos = e.GetPosition(null);
                if (Math.Abs(pos.X - _dragStartPoint.X) > 4 || Math.Abs(pos.Y - _dragStartPoint.Y) > 4)
                {
                    StartNativeDragOut(_heldFiles.ToArray());
                }
            }
        }

        private void StartNativeDragOut(string[] paths)
        {
            try
            {
                _isDraggingOut = true;
                var data = new DataObject(DataFormats.FileDrop, paths);
                DragDrop.DoDragDrop(this, data, DragDropEffects.Copy | DragDropEffects.Move);
            }
            finally
            {
                _isDraggingOut = false;
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            _heldFiles.Clear();
            RefreshUI();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Hide();
    }
}
