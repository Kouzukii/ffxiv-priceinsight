using System;
using System.Runtime.InteropServices;
using Dalamud.Game.Gui;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PriceInsight {
    // Taken mostly from https://github.com/Caraxi/SimpleTweaksPlugin under the terms of AGPL3
    public class Helper {
        public static unsafe void WriteSeString(byte** startPtr, IntPtr alloc, SeString seString) {
            if (startPtr == null) return;
            var start = *(startPtr);
            if (start == null) return;
            if (start == (byte*)alloc) return;
            WriteSeString((byte*)alloc, seString);
            *startPtr = (byte*)alloc;
        }

        public static unsafe SeString ReadSeString(byte** startPtr) {
            if (startPtr == null) return SeString.Empty;
            var start = *(startPtr);
            if (start == null) return SeString.Empty;
            return ReadSeString(start);
        }

        public static unsafe SeString ReadSeString(byte* ptr) {
            var offset = 0;
            while (true) {
                var b = *(ptr + offset);
                if (b == 0) {
                    break;
                }

                offset += 1;
            }

            var bytes = new byte[offset];
            Marshal.Copy(new IntPtr(ptr), bytes, 0, offset);
            return SeString.Parse(bytes);
        }

        public static unsafe void WriteSeString(byte* dst, SeString s) {
            var bytes = s.Encode();
            for (var i = 0; i < bytes.Length; i++) {
                *(dst + i) = bytes[i];
            }

            *(dst + bytes.Length) = 0;
        }

        public enum Step {
            Parent,
            Child,
            Previous,
            Next,
            PrevFinal
        }

        public static unsafe void SetControlsSectionHeight(GameGui gui, int height) {
            var heightShort = (ushort)height;
            var tooltipUi = (AtkUnitBase*)gui.GetAddonByName("ItemDetail", 1);
            if (tooltipUi == null) return;
            var bg = GetResNodeByPath(tooltipUi->RootNode, Step.Child, Step.PrevFinal, Step.Child, Step.Child);
            if (bg != null) bg->Height = heightShort;
        }

        public static unsafe AtkResNode* GetResNodeByPath(AtkResNode* root, params Step[] steps) {
            var current = root;
            foreach (var step in steps) {
                if (current == null) return null;

                current = step switch {
                    Step.Parent => current->ParentNode,
                    Step.Child => (ushort)current->Type >= 1000 ? ((AtkComponentNode*)current)->Component->UldManager.RootNode : current->ChildNode,
                    Step.Next => current->NextSiblingNode,
                    Step.Previous => current->PrevSiblingNode,
                    Step.PrevFinal => FinalPreviousNode(current),
                    _ => null,
                };
            }

            return current;
        }

        private static unsafe AtkResNode* FinalPreviousNode(AtkResNode* node) {
            while (node->PrevSiblingNode != null) node = node->PrevSiblingNode;
            return node;
        }
    }
}