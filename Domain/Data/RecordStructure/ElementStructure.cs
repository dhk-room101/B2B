namespace TESVSnip.Domain.Data.RecordStructure
{
    using System;

    public class ElementStructure
    {
        public int CondID;

        public string FormIDType;

        public string desc;

        public string[] flags;

        public int group;

        public bool hexview;

        public bool hexviewwithdec;

        public bool multiline;

        public string name;
        
        public string attachTo;
        
        public string owner;

        public bool notininfo;

        public bool optional;

        public string[] options;

        public int repeat;

        public string funcr;

        public string funcw;

        public ElementValueType type;

        public ElementStructure()
        {
            this.name = "DATA";
            this.attachTo = "DATA";
            this.owner = "DATA";
            this.desc = "Data";
            this.@group = 0;
            this.hexview = true;
            this.hexviewwithdec = false;
            this.notininfo = true;
            this.optional = true;
            this.options = null;
            this.flags = null;
            this.repeat = 0;
            this.CondID = 0;
            this.FormIDType = null;
            this.multiline = false;
            this.funcr = string.Empty;
            this.funcw = string.Empty;
            this.type = ElementValueType.Blob;
        }

        public ElementStructure(SubrecordElement node)
        {
            this.name = node.name;
            this.attachTo = node.attachTo;
            this.desc = node.desc;
            this.@group = node.group;
            this.hexview = node.hexview;
            this.hexviewwithdec = node.hexviewwithdec;
            this.notininfo = node.notininfo;
            this.optional = node.optional != 0;
            this.options = node.options == null ? new string[0] : node.options.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            this.flags = node.flags == null ? new string[0] : node.flags.Split(new[] { ';' });
            this.repeat = node.repeat;
            this.funcr = node.funcr;
            this.funcw = node.funcw;
            this.CondID = node.condid;
            if (this.optional || this.repeat > 0)
            {
                if (this.@group != 0)
                {
                    throw new RecordXmlException("Elements with a group attribute cant be marked optional or repeat");
                }
            }

            this.FormIDType = null;
            this.multiline = node.multiline;
            this.type = (ElementValueType)Enum.Parse(typeof(ElementValueType), node.type, true);
            switch (this.type)
            {
                case ElementValueType.FormID:
                    this.FormIDType = node.reftype;
                    break;
                case ElementValueType.Blob:
                    if (this.repeat > 0 || this.optional)
                    {
                        throw new RecordXmlException("blob type elements can't be marked with repeat or optional");
                    }

                    break;
            }
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(this.desc) || this.desc == this.name)
            {
                return this.name;
            }

            return string.Format("{0}: {1}", this.name, this.desc);
        }
    }
}