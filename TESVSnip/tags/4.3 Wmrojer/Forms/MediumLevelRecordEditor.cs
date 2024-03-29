using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using TESVSnip.Properties;
using TESVSnip.Windows.Controls;

namespace TESVSnip
{
    internal partial class MediumLevelRecordEditor : Form
    {
        private readonly SubRecord sr;
        private readonly SubrecordStructure ss;
        private readonly List<TextBox> boxes;
        private readonly List<ElementValueType> valueTypes;
        private readonly List<Panel> elements;
        private readonly dFormIDLookupS formIDLookup;
        private readonly dFormIDScan formIDScan;
        private readonly dLStringLookup strIDLookup;

        private readonly Dictionary<int, string> removedStrings = new Dictionary<int, string>();

        private readonly int repeatcount;

        private readonly Dictionary<string, string[]> cachedFormIDs = new Dictionary<string, string[]>();

        private class cbTag
        {
            public readonly int group;
            public readonly TextBox textBox;

            public cbTag(int group, TextBox textBox)
            {
                this.group = group;
                this.textBox = textBox;
            }
        }

        private class bTag
        {
            public readonly TextBox formID;
            public readonly TextBox edid;

            public bTag(TextBox form, TextBox edid)
            {
                formID = form;
                this.edid = edid;
            }
        }

        private class lTag
        {
            public readonly TextBox id;
            public TextBox str;
            public CheckBox cb;
            public byte[] data;
            public int offset;
            public readonly string disp;
            public readonly bool isString;

            public lTag(TextBox id, string disp, byte[] data, int offset, bool isString)
            {
                this.id = id;
                this.data = data;
                this.offset = offset;
                this.disp = disp;
                str = null;
                cb = null;
                this.isString = isString;
            }
        }

        private class comboBoxItem
        {
            public readonly string name;
            public readonly string value;

            public comboBoxItem(string name, string value)
            {
                this.name = name;
                this.value = value;
            }

            public override string ToString()
            {
                return name;
            }
        }

        private class repeatCbTag
        {
            public readonly TextBox tb;
            public readonly int panel;

            public repeatCbTag(TextBox tb, int panel)
            {
                this.tb = tb;
                this.panel = panel;
            }
        }

        private void AddElement(ElementStructure es)
        {
            int a = -1, b = 0, c = 0;
            AddElement(es, ref a, null, ref b, ref c);
        }

        private void AddElement(ElementStructure es, ref int offset, byte[] data, ref int groupOffset,
                                ref int CurrentGroup)
        {
            var panel1 = new Panel();
            panel1.AutoSize = true;
            panel1.Width = fpanel1.Width - 10;
            panel1.Height = 1;
            panel1.Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Left | AnchorStyles.Bottom;
            int ypos = 0;
            uint flagValue = 0; // value if flags is set
            bool hasFlags = (es.options.Length == 0 && es.flags.Length > 1);

            var tb = new TextBox();
            boxes.Add(tb);
            if (es.group != 0)
            {
                var cb = new CheckBox();
                cb.Text = "Use this value?";
                panel1.Controls.Add(cb);
                cb.Location = new Point(10, ypos);
                ypos += 24;
                cb.Tag = new cbTag(es.group, tb);
                if (CurrentGroup != es.group) cb.Checked = true;
                else tb.Enabled = false;
                cb.CheckedChanged += CheckBox_CheckedChanged;
            }
            if (es.optional || es.repeat > 0 && repeatcount > 0)
            {
                var cb = new CheckBox();
                cb.Text = "Use this value?";
                panel1.Controls.Add(cb);
                cb.Location = new Point(10, ypos);
                ypos += 24;
                cb.Tag = new repeatCbTag(tb, elements.Count);
                if (data == null)
                {
                    tb.Enabled = false;
                }
                else
                {
                    cb.Checked = true;
                }
                cb.CheckedChanged += RepeatCheckBox_CheckedChanged;
            }
            if ((CurrentGroup == 0 && es.group != 0) || (CurrentGroup != 0 && es.group != 0 && CurrentGroup != es.group))
            {
                CurrentGroup = es.group;
                groupOffset = offset;
            }
            else if (CurrentGroup != 0 && es.group == 0)
            {
                CurrentGroup = 0;
            }
            else if (CurrentGroup != 0 && CurrentGroup == es.group)
            {
                offset = groupOffset;
            }

            valueTypes.Add(es.type);
            if (data != null)
            {
                switch (es.type)
                {
                    case ElementValueType.UInt:
                        {
                            var v = TypeConverter.h2i(data[offset], data[offset + 1], data[offset + 2], data[offset + 3]);
                            flagValue = v;
                            tb.Text = hasFlags || es.hexview ? "0x" + v.ToString("X8") : v.ToString();
                            offset += 4;
                        }
                        break;
                    case ElementValueType.Int:
                        {
                            var v = TypeConverter.h2si(data[offset], data[offset + 1], data[offset + 2],
                                                       data[offset + 3]);
                            flagValue = (uint) v;
                            tb.Text = hasFlags || es.hexview ? "0x" + v.ToString("X8") : v.ToString();
                            offset += 4;
                        }
                        break;
                    case ElementValueType.FormID:
                        tb.Text =
                            TypeConverter.h2i(data[offset], data[offset + 1], data[offset + 2], data[offset + 3]).
                                ToString("X8");
                        offset += 4;
                        break;
                    case ElementValueType.Float:
                        tb.Text =
                            TypeConverter.h2f(data[offset], data[offset + 1], data[offset + 2], data[offset + 3]).
                                ToString();
                        offset += 4;
                        break;
                    case ElementValueType.UShort:
                        {
                            var v = TypeConverter.h2s(data[offset], data[offset + 1]);
                            flagValue = v;
                            tb.Text = hasFlags || es.hexview ? "0x" + v.ToString("X4") : v.ToString();
                            offset += 2;
                        }
                        break;
                    case ElementValueType.Short:
                        {
                            var v = TypeConverter.h2ss(data[offset], data[offset + 1]);
                            flagValue = (uint) v;
                            tb.Text = hasFlags || es.hexview ? "0x" + v.ToString("X4") : v.ToString();
                            offset += 2;
                        }
                        break;
                    case ElementValueType.Byte:
                        {
                            var v = data[offset];
                            flagValue = v;
                            tb.Text = hasFlags || es.hexview ? "0x" + v.ToString("X2") : v.ToString();
                            offset++;
                        }
                        break;
                    case ElementValueType.SByte:
                        {
                            var v = (sbyte) data[offset];
                            flagValue = (uint) v;
                            tb.Text = hasFlags || es.hexview ? "0x" + v.ToString("X2") : v.ToString();
                            offset++;
                        }
                        break;
                    case ElementValueType.String:
                        {
                            string s = "";
                            while (data[offset] != 0) s += (char) data[offset++];
                            offset++;
                            tb.Text = s;
                            tb.Width += 200;
                        }
                        break;
                    case ElementValueType.BString:
                        {
                            int len = TypeConverter.h2s(data[offset], data[offset + 1]);
                            string s = Encoding.CP1252.GetString(data, offset + 2, len);
                            offset = offset + (2 + len);
                            tb.Text = s;
                            tb.Width += 200;
                        }
                        break;
                    case ElementValueType.IString:
                        {
                            int len = TypeConverter.h2si(data[offset], data[offset + 1], data[offset + 2], data[offset + 3]);
                            string s = Encoding.CP1252.GetString(data, offset + 4, len);
                            offset = offset + (4 + len);
                            tb.Text = s;
                            tb.Width += 200;
                        }
                        break;
                    case ElementValueType.LString:
                        {
                            int left = data.Length - offset;
                            uint id = (left < 4)
                                          ? 0
                                          : TypeConverter.h2i(data[offset], data[offset + 1], data[offset + 2],
                                                              data[offset + 3]);
                            bool isString = TypeConverter.IsLikelyString(new ArraySegment<byte>(data, offset, left));
                            int strOffset = offset;
                            string s = null;
                            if (isString)
                            {
                                s = TypeConverter.GetString(new ArraySegment<byte>(data, offset, data.Length - offset));
                                tb.Text = 0.ToString("X8");
                                offset += s.Length;
                            }
                            else
                            {
                                offset += 4;
                                tb.Text = id.ToString("X8");
                                if (strIDLookup != null)
                                    s = strIDLookup(id);
                            }
                            tb.Tag = new lTag(tb, s, data, strOffset, isString);
                        }
                        break;
                    case ElementValueType.Str4:
                        {
                            string s = Encoding.CP1252.GetString(data, offset, 4);
                            offset += 4;
                            tb.MaxLength = 4;
                            tb.Text = s;
                        }
                        break;
                    default:
                        throw new ApplicationException();
                }
            }
            else
            {
                if (es.type == ElementValueType.String || es.type == ElementValueType.BString
                    || es.type == ElementValueType.LString || es.type == ElementValueType.IString)
                    tb.Width += 200;
                if (removedStrings.ContainsKey(boxes.Count - 1)) tb.Text = removedStrings[boxes.Count - 1];
            }
            var l = new Label();
            l.AutoSize = true;
            string tmp = es.type.ToString();
            l.Text = tmp + ": " + es.name + (!string.IsNullOrEmpty(es.desc) ? (" (" + es.desc + ")") : "");
            panel1.Controls.Add(tb);
            tb.Location = new Point(10, ypos);
            if (es.multiline)
            {
                tb.Multiline = true;
                ypos += tb.Height*5;
                tb.Height *= 6;
            }
            panel1.Controls.Add(l);
            l.Location = new Point(tb.Right + 10, ypos + 3);
            string[] options = null;
            if (es.type == ElementValueType.FormID)
            {
                ypos += 28;
                var b = new Button();
                b.Text = "FormID lookup";
                b.Click += LookupFormID_Click;
                panel1.Controls.Add(b);
                b.Location = new Point(20, ypos);
                var tb2 = new TextBox();
                tb2.Width += 200;
                tb2.ReadOnly = true;
                panel1.Controls.Add(tb2);
                tb2.Location = new Point(b.Right + 10, ypos);
                b.Tag = new bTag(tb, tb2);
                if (es.FormIDType != null)
                {
                    if (cachedFormIDs.ContainsKey(es.FormIDType))
                    {
                        options = cachedFormIDs[es.FormIDType];
                    }
                    else
                    {
                        options = formIDScan(es.FormIDType);
                        cachedFormIDs[es.FormIDType] = options;
                    }
                }
            }
            else if (es.type == ElementValueType.LString)
            {
                ypos += 24;
                var ltag = tb.Tag as lTag;

                ltag.cb = new CheckBox();
                ltag.cb.Width = ltag.cb.Height;
                ltag.cb.Checked = ltag.isString;
                panel1.Controls.Add(ltag.cb);
                ltag.cb.Location = new Point(8, ypos);

                ltag.str = new TextBox();
                //ltag.str.Font = this.baseFont;
                ltag.str.Width += (200 - ltag.cb.Width + 8);
                panel1.Controls.Add(ltag.str);
                ltag.str.Location = new Point(ltag.cb.Location.X + ltag.cb.Width + 8, ypos);
                ltag.str.Text = string.IsNullOrEmpty(ltag.disp) ? "" : ltag.disp;

                ypos += 24;
            }
            else if (es.options != null)
            {
                options = es.options;
            }
            if (options != null && options.Length > 0)
            {
                ypos += 28;
                var cmb = new ComboBox();
                cmb.Tag = tb;
                cmb.Width += 200;
                for (int j = 0; j < options.Length; j += 2)
                {
                    cmb.Items.Add(new comboBoxItem(options[j], options[j + 1]));
                }
                cmb.KeyPress += cb_KeyPress;
                cmb.ContextMenu = new ContextMenu();
                cmb.SelectedIndexChanged += cb_SelectedIndexChanged;
                panel1.Controls.Add(cmb);
                cmb.Location = new Point(20, ypos);
            }
            if (hasFlags) // add flags combo box to the side
            {
                var ccb = new FlagComboBox();
                ccb.Tag = tb;
                ccb.SetItems(es.flags);
                ccb.SetState(flagValue);
                ccb.TextChanged += delegate
                                       {
                                           uint value = ccb.GetState();
                                           var text = ccb.Tag as TextBox;
                                           text.Text = "0x" + value.ToString("X");
                                       };
                ccb.Location = new Point(l.Location.X + l.Width + 10, tb.Top);
                ccb.Width = Math.Max(ccb.Width, Width - 50 - (ccb.Location.X));
                ccb.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
                panel1.Controls.Add(ccb);
            }
            fpanel1.Controls.Add(panel1);
            elements.Add(panel1);
        }

        public MediumLevelRecordEditor(SubRecord sr, SubrecordStructure ss, dFormIDLookupS formIDLookup,
                                       dFormIDScan formIDScan, dLStringLookup strIDLookup)
        {
            InitializeComponent();
            Icon = Resources.tesv_ico;
            SuspendLayout();
            this.sr = sr;
            this.ss = ss;
            this.formIDLookup = formIDLookup;
            this.formIDScan = formIDScan;
            this.strIDLookup = strIDLookup;

            int offset = 0;
            byte[] data = sr.GetReadonlyData();
            boxes = new List<TextBox>(ss.elements.Length);
            valueTypes = new List<ElementValueType>(ss.elements.Length);
            elements = new List<Panel>();
            int groupOffset = 0;
            int CurrentGroup = 0;
            try
            {
                for (int i = 0; i < ss.elements.Length; i++)
                {
                    if (ss.elements[i].optional && offset == data.Length)
                    {
                        AddElement(ss.elements[i]);
                    }
                    else
                    {
                        AddElement(ss.elements[i], ref offset, data, ref groupOffset, ref CurrentGroup);
                        if (ss.elements[i].repeat > 0)
                        {
                            repeatcount++;
                            if (offset < data.Length) i--;
                        }
                    }
                }
                if (ss.elements[ss.elements.Length - 1].repeat > 0 && repeatcount > 0)
                {
                    AddElement(ss.elements[ss.elements.Length - 1]);
                }
            }
            catch
            {
                MessageBox.Show("The subrecord doesn't appear to conform to the expected structure.\n" +
                                "Saving is disabled, and the formatted information may be incorrect", "Warning");
                bSave.Enabled = false;
            }
            ResumeLayout();
        }

        private void cb_SelectedIndexChanged(object sender, EventArgs e)
        {
            var cmb = (ComboBox) sender;
            var cbi = (comboBoxItem) cmb.SelectedItem;
            ((TextBox) cmb.Tag).Text = cbi.value;
        }

        private void cb_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private bool CheckingChange;
        private bool IgnoreChange;

        private void CheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (IgnoreChange) return;
            var changed = (CheckBox) sender;
            if (changed.Checked == false)
            {
                if (!CheckingChange)
                {
                    IgnoreChange = true;
                    changed.Checked = true;
                    IgnoreChange = false;
                    return;
                }
                else
                {
                    TextBox toDisable = ((cbTag) changed.Tag).textBox;
                    foreach (Control c in fpanel1.Controls)
                    {
                        var tb = c as TextBox;
                        if (tb == toDisable)
                        {
                            toDisable.Enabled = false;
                            return;
                        }
                    }
                    throw new ApplicationException();
                }
            }
            int Group = ((cbTag) changed.Tag).group;
            TextBox toEnable = ((cbTag) changed.Tag).textBox;
            CheckingChange = true;
            foreach (Panel p in fpanel1.Controls)
            {
                foreach (Control c in p.Controls)
                {
                    var cb = c as CheckBox;
                    if (cb != null && cb != changed)
                    {
                        if (((cbTag) changed.Tag).group == Group) cb.Checked = false;
                    }
                    var tb = c as TextBox;
                    if (tb == toEnable)
                    {
                        toEnable.Enabled = true;
                    }
                }
            }
            CheckingChange = false;
        }

        private void RepeatCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            var cb = (CheckBox) sender;
            var tag = (repeatCbTag) cb.Tag;
            tag.tb.Enabled = cb.Checked;
            if (cb.Checked)
            {
                if (ss.elements[ss.elements.Length - 1].repeat > 0)
                    AddElement(ss.elements[ss.elements.Length - 1]);
            }
            else
            {
                for (int i = tag.panel + 1; i < elements.Count; i++)
                {
                    removedStrings[i] = boxes[i].Text;
                    fpanel1.Controls.Remove(elements[i]);
                }
                boxes.RemoveRange(tag.panel + 1, elements.Count - (tag.panel + 1));
                valueTypes.RemoveRange(tag.panel + 1, elements.Count - (tag.panel + 1));
                elements.RemoveRange(tag.panel + 1, elements.Count - (tag.panel + 1));
            }
        }

        private void bCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void bSave_Click(object sender, EventArgs e)
        {
            var bytes = new List<byte>();
            for (int j = 0; j < boxes.Count; j++)
            {
                var vt = valueTypes[j];
                string tbText = boxes[j].Text;
                NumberStyles numStyle = NumberStyles.Any;
                if (tbText.StartsWith("0x"))
                {
                    numStyle = NumberStyles.HexNumber;
                    tbText = tbText.Substring(2);
                }
                if (!boxes[j].Enabled) continue;
                switch (vt)
                {
                    case ElementValueType.Byte:
                        {
                            byte b;
                            if (!byte.TryParse(tbText, numStyle, null, out b))
                            {
                                MessageBox.Show("Invalid byte: " + tbText, "Error");
                                return;
                            }
                            bytes.Add(b);
                            break;
                        }
                    case ElementValueType.Short:
                        {
                            short s;
                            if (!short.TryParse(tbText, numStyle, null, out s))
                            {
                                MessageBox.Show("Invalid short: " + tbText, "Error");
                                return;
                            }
                            byte[] conv = TypeConverter.ss2h(s);
                            bytes.Add(conv[0]);
                            bytes.Add(conv[1]);
                            break;
                        }
                    case ElementValueType.UShort:
                        {
                            ushort s;
                            if (!ushort.TryParse(tbText, numStyle, null, out s))
                            {
                                MessageBox.Show("Invalid ushort: " + tbText, "Error");
                                return;
                            }
                            byte[] conv = TypeConverter.s2h(s);
                            bytes.Add(conv[0]);
                            bytes.Add(conv[1]);
                            break;
                        }
                    case ElementValueType.Int:
                        {
                            int i;
                            if (!int.TryParse(tbText, numStyle, null, out i))
                            {
                                MessageBox.Show("Invalid int: " + tbText, "Error");
                                return;
                            }
                            byte[] conv = TypeConverter.si2h(i);
                            bytes.AddRange(conv);
                            break;
                        }
                    case ElementValueType.UInt:
                        {
                            uint i;
                            if (!uint.TryParse(tbText, numStyle, null, out i))
                            {
                                MessageBox.Show("Invalid uint: " + tbText, "Error");
                                return;
                            }
                            byte[] conv = TypeConverter.i2h(i);
                            bytes.AddRange(conv);
                            break;
                        }
                    case ElementValueType.Float:
                        {
                            float f;
                            if (!float.TryParse(tbText, numStyle, null, out f))
                            {
                                MessageBox.Show("Invalid float: " + tbText, "Error");
                                return;
                            }
                            byte[] conv = TypeConverter.f2h(f);
                            bytes.AddRange(conv);
                            break;
                        }
                    case ElementValueType.FormID:
                        {
                            uint i;
                            if (!uint.TryParse(tbText, NumberStyles.AllowHexSpecifier, null, out i))
                            {
                                MessageBox.Show("Invalid formID: " + tbText, "Error");
                                return;
                            }
                            byte[] conv = TypeConverter.i2h(i);
                            bytes.AddRange(conv);
                            break;
                        }
                    case ElementValueType.String:
                        {
                            byte[] conv = System.Text.Encoding.Default.GetBytes(tbText);
                            bytes.AddRange(conv);
                            bytes.Add(0);
                            break;
                        }
                    case ElementValueType.BString:
                        {
                            bytes.AddRange(TypeConverter.s2h((ushort) tbText.Length));
                            bytes.AddRange(System.Text.Encoding.Default.GetBytes(tbText));
                            break;
                        }
                    case ElementValueType.IString:
                        {
                            bytes.AddRange(TypeConverter.si2h(tbText.Length));
                            bytes.AddRange(System.Text.Encoding.Default.GetBytes(tbText));
                            break;
                        }
                    case ElementValueType.LString:
                        {
                            uint i;
                            var ltag = boxes[j].Tag as lTag;
                            if (ltag != null)
                            {
                                if (!ltag.cb.Checked)
                                {
                                    if (!uint.TryParse(ltag.id.Text, NumberStyles.AllowHexSpecifier, null, out i))
                                    {
                                        MessageBox.Show("Invalid string id: " + ltag.id.Text, "Error");
                                        return;
                                    }
                                    byte[] conv = TypeConverter.i2h(i);
                                    bytes.AddRange(conv);
                                }
                                else
                                {
                                    byte[] conv = System.Text.Encoding.Default.GetBytes(ltag.str.Text);
                                    bytes.AddRange(conv);
                                    bytes.Add(0);
                                }
                            }
                            break;
                        }
                    case ElementValueType.Str4:
                        {
                            var txtbytes = new byte[] {0x32, 0x32, 0x32, 0x32};
                            System.Text.Encoding.Default.GetBytes(tbText, 0, Math.Min(4, tbText.Length), txtbytes, 0);
                            bytes.AddRange(txtbytes);
                        }
                        break;
                    default:
                        throw new ApplicationException();
                }
            }
            sr.SetData(bytes.ToArray());
            Close();
        }

        private void LookupFormID_Click(object sender, EventArgs e)
        {
            var tag = (bTag) ((Button) sender).Tag;
            tag.edid.Text = formIDLookup(tag.formID.Text);
        }
    }
}