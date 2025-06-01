using UnityEngine;

public static class ConstantData
{
    // Constants for the chessboard
    public static int GRID_SIZE = 8;
    public static char[] FILES = { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H' };

    // Cell colors
    public static Color CELL_LIGHT_COLOR = new Color32(232, 237, 249, 255); // Light color
    public static Color CELL_LIGHT_COLOR_SELECT = new Color32(177, 166, 252, 255); // Light color when selected

    public static Color CELL_DARK_COLOR = new Color32(183, 192, 216, 255); // Dark color
    public static Color CELL_DARK_COLOR_SELECT = new Color32(152, 144, 236, 255); // Dark color when selected
}
