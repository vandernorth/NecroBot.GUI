using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;

/// <summary>
/// This class is an implementation of the 'IComparer' interface.
/// </summary>
public class ListViewColumnSorter : IComparer
{
    /// <summary>
    /// Specifies the column to be sorted
    /// </summary>
    private int ColumnToSort;
    /// <summary>
    /// Specifies the order in which to sort (i.e. 'Ascending').
    /// </summary>
    private SortOrder OrderOfSort;
    /// <summary>
    /// Case insensitive comparer object
    /// </summary>
    private CaseInsensitiveComparer ObjectCompare;

    /// <summary>
    /// Class constructor.  Initializes various elements
    /// </summary>
    public ListViewColumnSorter()
    {
        // Initialize the column to '0'
        ColumnToSort = 0;

        // Initialize the sort order to 'none'
        OrderOfSort = SortOrder.None;

        // Initialize the CaseInsensitiveComparer object
        ObjectCompare = new CaseInsensitiveComparer();


    }

    /// <summary>
    /// This method is inherited from the IComparer interface.  It compares the two objects passed using a case insensitive comparison.
    /// </summary>
    /// <param name="x">First object to be compared</param>
    /// <param name="y">Second object to be compared</param>
    /// <returns>The result of the comparison. "0" if equal, negative if 'x' is less than 'y' and positive if 'x' is greater than 'y'</returns>
    public int Compare(object x, object y)
    {
        int compareResult;
        ListViewItem listviewX, listviewY;

        // Cast the objects to be compared to ListViewItem objects
        listviewX = (ListViewItem)x;
        listviewY = (ListViewItem)y;

        // Compare the two items
        string a = listviewX.SubItems[ColumnToSort].Text;
        string b = listviewY.SubItems[ColumnToSort].Text;
        compareResult = (new StringNum(a).CompareTo(new StringNum(b)));
        //compareResult = ObjectCompare.Compare(, listviewY.SubItems[ColumnToSort].Text);

        // Calculate correct return value based on object comparison
        if (OrderOfSort == SortOrder.Ascending)
        {
            // Ascending sort is selected, return normal result of compare operation
            return compareResult;
        }
        else if (OrderOfSort == SortOrder.Descending)
        {
            // Descending sort is selected, return negative result of compare operation
            return (-compareResult);
        }
        else
        {
            // Return '0' to indicate they are equal
            return 0;
        }
    }

    /// <summary>
    /// Gets or sets the number of the column to which to apply the sorting operation (Defaults to '0').
    /// </summary>
    public int SortColumn
    {
        set
        {
            ColumnToSort = value;
        }
        get
        {
            return ColumnToSort;
        }
    }

    /// <summary>
    /// Gets or sets the order of sorting to apply (for example, 'Ascending' or 'Descending').
    /// </summary>
    public SortOrder Order
    {
        set
        {
            OrderOfSort = value;
        }
        get
        {
            return OrderOfSort;
        }
    }

    public class StringNum : IComparable<StringNum>
    {

        private List<string> _strings;
        private List<ulong> _numbers;

        public StringNum(string value)
        {
            _strings = new List<string>();
            _numbers = new List<ulong>();
            int pos = 0;
            bool number = false;
            while (pos < value.Length)
            {
                int len = 0;
                while (pos + len < value.Length && Char.IsDigit(value[pos + len]) == number)
                {
                    len++;
                }
                if (number)
                {
                    _numbers.Add(ulong.Parse(value.Substring(pos, len)));
                }
                else {
                    _strings.Add(value.Substring(pos, len));
                }
                pos += len;
                number = !number;
            }
        }

        public int CompareTo(StringNum other)
        {
            int index = 0;
            while (index < _strings.Count && index < other._strings.Count)
            {
                int result = _strings[index].CompareTo(other._strings[index]);
                if (result != 0) return result;
                if (index < _numbers.Count && index < other._numbers.Count)
                {
                    result = _numbers[index].CompareTo(other._numbers[index]);
                    if (result != 0) return result;
                }
                else {
                    return index == _numbers.Count && index == other._numbers.Count ? 0 : index == _numbers.Count ? -1 : 1;
                }
                index++;
            }
            return index == _strings.Count && index == other._strings.Count ? 0 : index == _strings.Count ? -1 : 1;
        }

    }

}