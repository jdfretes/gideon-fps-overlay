// Resuelve la ambigüedad entre System.Windows.Application (WPF) y
// System.Windows.Forms.Application que surge al usar UseWPF + UseWindowsForms juntos.
global using Application = System.Windows.Application;
global using MessageBox = System.Windows.MessageBox;
global using MessageBoxButton = System.Windows.MessageBoxButton;
global using MessageBoxImage = System.Windows.MessageBoxImage;
