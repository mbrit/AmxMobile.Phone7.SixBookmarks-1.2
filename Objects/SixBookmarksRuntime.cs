using System;
using Microsoft.Phone.Tasks;

namespace AmxMobile.Phone7.SixBookmarks
{
    public class SixBookmarksRuntime
    {
        /// <summary>
		/// Private field to hold singleton instance.
		/// </summary>
		private static SixBookmarksRuntime _current = new SixBookmarksRuntime();
		
		// settings...
        public SimpleXmlPropertyBag Settings { get; private set; }

		/// <summary>
		/// Private constructor.
		/// </summary>
		private SixBookmarksRuntime()
		{
            // settings...
            this.Settings = SimpleXmlPropertyBag.Load("Settings.xml", false);

            // register the entity type...
            EntityType bookmark = new EntityType(typeof(Bookmark), "Bookmark");
            bookmark.AddField(Bookmark.BookmarkIdKey, Bookmark.BookmarkIdKey, DataType.Int32, -1).IsKey = true;
            bookmark.AddField(Bookmark.NameKey, Bookmark.NameKey, DataType.String, 128);
            bookmark.AddField(Bookmark.UrlKey, Bookmark.UrlKey, DataType.String, 128);
            bookmark.AddField(Bookmark.OrdinalKey, Bookmark.OrdinalKey, DataType.Int32, -1);
            bookmark.AddField(Bookmark.IsLocalModifiedKey, Bookmark.IsLocalModifiedKey, DataType.Boolean, -1).IsOnServer = false;
            bookmark.AddField(Bookmark.IsLocalDeletedKey, Bookmark.IsLocalDeletedKey, DataType.Boolean, -1).IsOnServer = false;
            EntityType.RegisterEntityType(bookmark);

            // create a tombstone data entity type...
            EntityType tombstone = new EntityType(typeof(TombstoneData), "TombstoneData");
            tombstone.AddField(TombstoneData.TombstoneDataIdKey, TombstoneData.TombstoneDataIdKey, DataType.Int32, -1).IsKey = true;
            tombstone.AddField(TombstoneData.NameKey, TombstoneData.NameKey, DataType.String, 64);
            tombstone.AddField(TombstoneData.ValueKey, TombstoneData.ValueKey, DataType.String, 256);
            EntityType.RegisterEntityType(tombstone);
        }
						
		/// <summary>
		/// Gets the singleton instance of <see cref="SixBookmarksRuntime">SixBookmarksRuntime</see>.
		/// </summary>
		internal static SixBookmarksRuntime Current
		{
			get
			{
				if(_current == null)
					throw new ObjectDisposedException("SixBookmarksRuntime");
				return _current;
			}
		}

        internal void ShowUrl(string url)
        {
            WebBrowserTask task = new WebBrowserTask();
            task.URL = url;
            task.Show();
        }

        internal static void EnsureInitialized()
        {
            // called after we have been "tombstaned"... all we have to do here is check that we have Current defined.  this 
            // will reestablish our state...
            if (Current == null)
                throw new InvalidOperationException("'Current' is null.");
        }
    }
}
