using ARMeilleure.CodeGen.RegisterAllocators;
using ARMeilleure.Common;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.Translation.AOT;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ARMeilleure.CodeGen.X86
{
    class CodeGenContext
    {
        private Stream _stream;

        private AotInfo _aotInfo;

        //public int StreamOffset => (int)_stream.Length;

        public AllocationResult AllocResult { get; }

        public Assembler Assembler { get; }

        public BasicBlock CurrBlock { get; private set; }

        public int CallArgsRegionSize { get; }
        public int XmmSaveRegionSize  { get; }

        private long[] _blockOffsets;

        private struct Jump
        {
            public bool IsConditional { get; }

            public X86Condition Condition { get; }

            public BasicBlock Target { get; }

            public long JumpPosition { get; }

            public long RelativeOffset { get; set; }

            public int InstSize { get; }

            public Jump(BasicBlock target, long jumpPosition)
            {
                IsConditional = false;
                Condition     = 0;
                Target        = target;
                JumpPosition  = jumpPosition;

                RelativeOffset = 0;

                InstSize = 5;
            }

            public Jump(X86Condition condition, BasicBlock target, long jumpPosition)
            {
                IsConditional = true;
                Condition     = condition;
                Target        = target;
                JumpPosition  = jumpPosition;

                RelativeOffset = 0;

                InstSize = 6;
            }
        }

        private List<Jump> _jumps;

        private X86Condition _jNearCondition;

        private long _jNearPosition;
        private int  _jNearLength;

        public CodeGenContext(Stream stream, AllocationResult allocResult, int maxCallArgs, int blocksCount, AotInfo aotInfo = null)
        {
            _stream = stream;

            AllocResult = allocResult;

            Assembler = new Assembler(stream, aotInfo);

            CallArgsRegionSize = GetCallArgsRegionSize(allocResult, maxCallArgs, out int xmmSaveRegionSize);
            XmmSaveRegionSize  = xmmSaveRegionSize;

            _blockOffsets = new long[blocksCount];

            _jumps = new List<Jump>();

            _aotInfo = aotInfo;
        }

        private int GetCallArgsRegionSize(AllocationResult allocResult, int maxCallArgs, out int xmmSaveRegionSize)
        {
            // We need to add 8 bytes to the total size, as the call to this
            // function already pushed 8 bytes (the return address).
            int intMask = CallingConvention.GetIntCalleeSavedRegisters() & allocResult.IntUsedRegisters;
            int vecMask = CallingConvention.GetVecCalleeSavedRegisters() & allocResult.VecUsedRegisters;

            xmmSaveRegionSize = BitUtils.CountBits(vecMask) * 16;

            int calleeSaveRegionSize = BitUtils.CountBits(intMask) * 8 + xmmSaveRegionSize + 8;

            int argsCount = maxCallArgs;

            if (argsCount < 0)
            {
                // When the function has no calls, argsCount is -1.
                // In this case, we don't need to allocate the shadow space.
                argsCount = 0;
            }
            else if (argsCount < 4)
            {
                // The ABI mandates that the space for at least 4 arguments
                // is reserved on the stack (this is called shadow space).
                argsCount = 4;
            }

            int frameSize = calleeSaveRegionSize + allocResult.SpillRegionSize;

            // TODO: Instead of always multiplying by 16 (the largest possible size of a variable,
            // since a V128 has 16 bytes), we should calculate the exact size consumed by the
            // arguments passed to the called functions on the stack.
            int callArgsAndFrameSize = frameSize + argsCount * 16;

            // Ensure that the Stack Pointer will be aligned to 16 bytes.
            callArgsAndFrameSize = (callArgsAndFrameSize + 0xf) & ~0xf;

            return callArgsAndFrameSize - frameSize;
        }

        public void EnterBlock(BasicBlock block)
        {
            _blockOffsets[block.Index] = _stream.Position;

            CurrBlock = block;
        }

        public void JumpTo(BasicBlock target)
        {
            _jumps.Add(new Jump(target, _stream.Position));

            WritePadding(5);
        }

        public void JumpTo(X86Condition condition, BasicBlock target)
        {
            _jumps.Add(new Jump(condition, target, _stream.Position));

            WritePadding(6);
        }

        public void JumpToNear(X86Condition condition)
        {
            _jNearCondition = condition;
            _jNearPosition  = _stream.Position;
            _jNearLength    = Assembler.GetJccLength(0);

            _stream.Seek(_jNearLength, SeekOrigin.Current);
        }

        public void JumpHere()
        {
            long currentPosition = _stream.Position;

            _stream.Seek(_jNearPosition, SeekOrigin.Begin);

            long offset = currentPosition - (_jNearPosition + _jNearLength);

            Debug.Assert(_jNearLength == Assembler.GetJccLength(offset < 0 ? offset - 6 : offset), "Relative offset doesn't fit on near jump.");

            Assembler.Jcc(_jNearCondition, offset);

            _stream.Seek(currentPosition, SeekOrigin.Begin);
        }

        private void WritePadding(int size)
        {
            while (size-- > 0)
            {
                _stream.WriteByte(0);
            }
        }

        public byte[] GetCode()
        {
            // Write jump relative offsets.
            bool modified;

            do
            {
                modified = false;

                for (int index = 0; index < _jumps.Count; index++)
                {
                    Jump jump = _jumps[index];

                    long jumpTarget = _blockOffsets[jump.Target.Index];

                    long offset = jumpTarget - jump.JumpPosition;

                    offset -= jump.InstSize;

                    if (jump.RelativeOffset != offset)
                    {
                        modified = true;
                    }

                    jump.RelativeOffset = offset;

                    _jumps[index] = jump;
                }
            }
            while (modified);

            // Write the code into a new stream.
            _stream.Seek(0, SeekOrigin.Begin);

            using (MemoryStream codeStream = new MemoryStream())
            {
                Assembler assembler = new Assembler(codeStream);

                byte[] buffer;

                for (int index = 0; index < _jumps.Count; index++)
                {
                    Jump jump = _jumps[index];

                    buffer = new byte[jump.JumpPosition - _stream.Position];

                    _stream.Read(buffer, 0, buffer.Length);
                    _stream.Seek(jump.InstSize, SeekOrigin.Current);

                    codeStream.Write(buffer);

                    if (jump.IsConditional)
                    {
                        assembler.Jcc(jump.Condition, jump.RelativeOffset);
                    }
                    else
                    {
                        assembler.Jmp(jump.RelativeOffset);
                    }
                }

                buffer = new byte[_stream.Length - _stream.Position];

                _stream.Read(buffer, 0, buffer.Length);

                codeStream.Write(buffer);

                if (_aotInfo != null)
                {
                    if ((int)codeStream.Length >= Aot.MinCodeLengthToSave)
                    {
                        _aotInfo.WriteCode(codeStream);
                    }
                }

                return codeStream.ToArray();
            }
        }
    }
}
