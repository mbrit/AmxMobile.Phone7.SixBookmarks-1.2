using System;
using System.Collections.Generic;

namespace AmxMobile.Phone7.SixBookmarks
{
    internal class BookmarksService : ODataServiceProxy
    {
        internal BookmarksService()
            : base("Bookmarks.svc/")
        {
        }
    }
}
