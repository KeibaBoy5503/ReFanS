using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ReFanS
{
    // Masonry Layout Panel for WPF ( Wall Logic )
    public class MasonryPanel : Panel
    {
        public static readonly DependencyProperty ColumnWidthProperty =
            DependencyProperty.Register(nameof(ColumnWidth), typeof(double), typeof(MasonryPanel),
                new FrameworkPropertyMetadata(200.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double ColumnWidth
        {
            get => (double)GetValue(ColumnWidthProperty);
            set => SetValue(ColumnWidthProperty, value);
        }

        // Top-Left alignment by default to ensure items are placed starting from the top-left corner
        public MasonryPanel()
        {
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double panelWidth = double.IsInfinity(availableSize.Width) ? 1000 : availableSize.Width;
            double colWidth = Math.Max(100, ColumnWidth);
            int columns = Math.Max(1, (int)(panelWidth / colWidth));
            double[] colHeights = new double[columns];

            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(colWidth, double.PositiveInfinity));

                // Find the shortest column to place the child in, ensuring that items are stacked from top to bottom in each column
                int shortestCol = GetShortestColumnIndex(colHeights);
                colHeights[shortestCol] += child.DesiredSize.Height;
            }

            return new Size(panelWidth, colHeights.Length > 0 ? colHeights.Max() : 0);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double colWidth = Math.Max(100, ColumnWidth);
            int columns = Math.Max(1, (int)(finalSize.Width / colWidth));
            double[] colHeights = new double[columns];

            foreach (UIElement child in InternalChildren)
            {
                // Align items to the top-left corner of the panel, ensuring that they are arranged in a masonry layout
                int shortestCol = GetShortestColumnIndex(colHeights);
                double x = shortestCol * colWidth;
                double y = colHeights[shortestCol];
                double height = child.DesiredSize.Height;

                child.Arrange(new Rect(x, y, colWidth, height));
                colHeights[shortestCol] += height;
            }

            return new Size(finalSize.Width, colHeights.Length > 0 ? colHeights.Max() : finalSize.Height);
        }

        // Helper method to find the index of the shortest column, ensuring that items are placed in the column with the least height
        private int GetShortestColumnIndex(double[] colHeights)
        {
            int minIndex = 0;
            double minHeight = colHeights[0];

            for (int i = 1; i < colHeights.Length; i++)
            {
                // Select the column with the minimum height, allowing for a small tolerance to avoid floating-point precision issues
                if (colHeights[i] < minHeight - 0.1)
                {
                    minHeight = colHeights[i];
                    minIndex = i;
                }
            }
            return minIndex;
        }
    }
}