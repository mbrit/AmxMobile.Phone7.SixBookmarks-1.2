using System;
using System.Collections.Generic;

namespace AmxMobile.Phone7.SixBookmarks
{
    public class TombstoneData : Entity
    {
        public const string TombstoneDataIdKey = "TombstoneDataId";
        public const string NameKey = "Name";
        public const string ValueKey = "Value";

        public TombstoneData()
        {
        }

        public int TombstoneDataId
        {
            get
            {
                return this.GetInt32Value(TombstoneDataIdKey);
            }
            set
            {
                this.SetValue(TombstoneDataIdKey, value, SetReason.UserSet);
            }
        }

        public string Name
        {
            get
            {
                return this.GetStringValue(NameKey);
            }
            set
            {
                this.SetValue(NameKey, value, SetReason.UserSet);
            }
        }

        public string Value
        {
            get
            {
                return this.GetStringValue(ValueKey);
            }
            set
            {
                this.SetValue(ValueKey, value, SetReason.UserSet);
            }
        }

        private static DataBox GetDataBox()
        {
            EntityType et = EntityType.GetEntityType(typeof(TombstoneData));
            return new DataBox(et);
        }

        internal static TombstoneData GetTombstoneItem(string name, bool createIfNotFound)
        {
            DataBoxFilter filter = new DataBoxFilter(GetDataBox());
            filter.AddConstraint(NameKey, name);

            // return...
            TombstoneData data = filter.ExecuteEntity<TombstoneData>();
            if (data == null && createIfNotFound)
            {
                data = new TombstoneData();
                data.Name = name;
            }

            // return...
            return data;
        }
    }
}
