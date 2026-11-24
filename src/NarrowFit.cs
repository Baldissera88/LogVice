namespace LogVice
{
    using System;
    using System.Collections.Generic;

    // Decides which items of a row do not fit and fold away, for the header buttons and the counts of the main window.
    public static class NarrowFit
    {
        // widths: every item with its margins. foldOrder: the indexes of the items that may fold, the first to go first.
        // moreWidth: the room the More button takes once anything is folded. Items that are not in foldOrder stay.
        // Returns, for each item, whether it is folded.
        public static bool[] Hidden(int[] widths, int[] foldOrder, int moreWidth, int room)
        {
            bool[] hidden = new bool[widths.Length];
            int total = 0;
            foreach (int w in widths) total += w;
            if (total <= room) return hidden;
            total += moreWidth;
            foreach (int i in foldOrder)
            {
                if (total <= room) break;
                if (i < 0 || i >= widths.Length || hidden[i]) continue;
                hidden[i] = true;
                total -= widths[i];
            }
            return hidden;
        }

        // A size that does not go past the room there is, such as a dialog on a small or a scaled screen.
        public static int Capped(int size, int room)
        {
            return room > 0 && size > room ? room : size;
        }

        // Where a window of this size starts so that it stays inside the room from min to min + room, as near to want as it can.
        public static int Inside(int want, int size, int min, int room)
        {
            int max = min + room - size;
            if (want > max) want = max;
            return want < min ? min : want;
        }
    }

    // The buttons of the header, left to right, and which of them fold into the More menu when the window is narrow.
    public static class HeaderFold
    {
        public const string Back = "Back", Forward = "Forward", Start = "Start", Open = "Open", Browse = "Browse", Follow = "Follow",
            Live = "Live", Save = "Save", Stop = "Stop", Reload = "Reload", Lookup = "Lookup", Theme = "Theme", Settings = "Settings", Help = "Help";

        public static readonly string[] Order = { Back, Forward, Start, Open, Browse, Follow, Live, Save, Stop, Reload, Lookup, Theme, Settings, Help };

        // The first to fold first. Back, Forward, Start, Open, Settings, Help and Stop always stay.
        public static readonly string[] FoldOrder = { Reload, Lookup, Follow, Save, Browse, Live, Theme };

        // names and widths run in step: the buttons that are shown, with their margins. Returns the names that fold, first to go first.
        public static List<string> Folded(string[] names, int[] widths, int moreWidth, int room)
        {
            int[] order = new int[FoldOrder.Length];
            for (int i = 0; i < order.Length; i++) order[i] = Array.IndexOf(names, FoldOrder[i]);
            bool[] hidden = NarrowFit.Hidden(widths, order, moreWidth, room);
            List<string> list = new List<string>();
            foreach (int i in order) if (i >= 0 && hidden[i]) list.Add(names[i]);
            return list;
        }
    }
}
