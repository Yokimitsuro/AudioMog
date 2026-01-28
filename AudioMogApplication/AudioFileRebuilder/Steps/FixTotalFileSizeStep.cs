using System;
using System.Linq;
using AudioMog.Core;
using AudioMog.Core.Audio;

namespace AudioMog.Application.AudioFileRebuilder.Steps
{
	public class FixTotalFileSizeStep : ARebuilderStep
	{
		public override void Run(Blackboard blackboard)
		{
			FixTotalFileSize(blackboard.File, blackboard.FileBytes, blackboard.Logger);
		}
		private void FixTotalFileSize(AAudioBinaryFile file, byte[] fileBytes, IApplicationLogger logger)
		{
			var positionBeforeMagic = file.InnerFileStartOffset;
			var fileVersion = $"{file.Header.VersionMain}.{file.Header.VersionSub}";
			
			logger?.Log($"[FixTotalFileSizeStep] Processing file version {fileVersion}, InnerFileStartOffset={positionBeforeMagic}");
			
			var originalInnerSize = (uint)file.InnerFileBytes.Length;
			var changedInnerSize = fileBytes.Length - positionBeforeMagic - file.BytesAfterFile.Length;
			
			logger?.Log($"[FixTotalFileSizeStep] Original size: {originalInnerSize}, Changed size: {changedInnerSize}");
			
			var wroteIntoAudioBundle = WriteUintIfMatch(fileBytes, (int)positionBeforeMagic + 0x0c, originalInnerSize, (uint)changedInnerSize);
			if (wroteIntoAudioBundle)
				logger?.Log($"[FixTotalFileSizeStep] Updated size in header at offset 0x0C");
			
			//2025 format
			if (positionBeforeMagic >= 8)
			{
				var tightWrite1 = WriteUintIfMatch(fileBytes, (int)positionBeforeMagic - 4, originalInnerSize, (uint)changedInnerSize);
				var tightWrite2 = WriteUintIfMatch(fileBytes, (int)positionBeforeMagic - 8, originalInnerSize, (uint)changedInnerSize);
				if (tightWrite1 && tightWrite2)
				{
					logger?.Log($"[FixTotalFileSizeStep] Detected 2025 format, updated offsets -4 and -8");
					var originalWeirdSize = BitConverter.GetBytes(originalInnerSize + 16).Concat(new byte[] { 0x00, 0x00, 0x01, 0x00, 0x10, 0x00 }).ToArray();
					var hasMagic = ExtensionMethods.FindMagicFromEnd(fileBytes, originalWeirdSize, out var offset);
					if (hasMagic)
					{
						WriteUintIfMatch(fileBytes, (int)offset, originalInnerSize + 16, (uint)changedInnerSize + 16);
						logger?.Log($"[FixTotalFileSizeStep] Updated magic size at offset {offset}");
					}
					return;
				}
			}
			else
			{
				logger?.Log($"[FixTotalFileSizeStep] Skipping 2025 format check (offset < 8)");
			}
			
			//2019 format
			var wrote2019 = false;
			if (positionBeforeMagic >= 16)
			{
				var w1 = WriteUintIfMatch(fileBytes, (int)positionBeforeMagic - 16, originalInnerSize, (uint)changedInnerSize);
				var w2 = WriteUintIfMatch(fileBytes, (int)positionBeforeMagic - 12, originalInnerSize, (uint)changedInnerSize);
				wrote2019 = w1 || w2;
				if (wrote2019)
					logger?.Log($"[FixTotalFileSizeStep] Updated 2019 format offsets -16/-12");
			}
			if (positionBeforeMagic >= 44)
			{
				if (WriteUintIfMatch(fileBytes, (int)positionBeforeMagic - 44, originalInnerSize, (uint)changedInnerSize))
				{
					logger?.Log($"[FixTotalFileSizeStep] Updated 2019 format offset -44");
					wrote2019 = true;
				}
			}
			
			if (!wrote2019 && !wroteIntoAudioBundle)
			{
				logger?.Log($"[FixTotalFileSizeStep] No size fields were updated. This may be normal for sabf 2.1 format files.");
			}
		}
	}
}