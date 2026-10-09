// Reports how much of the program the project has actually analysed, per region.
//
// Usage:
//   analyzeHeadless.bat <projectDir> MGO2 -process MGO2.ELF -noanalysis \
//       -scriptPath <repo>\.ghidra_scripts -postScript Mgo2Coverage.java
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;

public class Mgo2Coverage extends GhidraScript {

	private static final long[][] WINDOWS = {
		{ 0x260000L, 0x270000L }, // the record manager this repository reads
		{ 0xa90000L, 0xab0000L }, // the connect FSM
		{ 0x000000L, 0x100000L },
		{ 0x100000L, 0x400000L },
	};

	@Override
	public void run() throws Exception {
		for (MemoryBlock block : currentProgram.getMemory().getBlocks()) {
			println(String.format("%-24s %s - %s  %s%s%s",
				block.getName(),
				block.getStart(), block.getEnd(),
				block.isInitialized() ? "init" : "uninit",
				block.isExecute() ? " exec" : "",
				block.isRead() ? " read" : ""));
		}

		long[] cited = { 0xaa1140L, 0x268f18L, 0x26ab58L, 0x268c58L, 0x2676e0L, 0x266f14L };
		for (long offset : cited) {
			Address address = toAddr(offset);
			MemoryBlock block = currentProgram.getMemory().getBlock(address);
			Instruction instruction = currentProgram.getListing().getInstructionAt(address);
			Instruction containing = currentProgram.getListing().getInstructionContaining(address);
			Function function = currentProgram.getFunctionManager().getFunctionContaining(address);
			String bytes = "--------";
			if (block != null && block.isInitialized() && block.contains(address)) {
				byte[] raw = new byte[4];
				try {
					currentProgram.getMemory().getBytes(address, raw);
					StringBuilder text = new StringBuilder();
					for (byte value : raw) {
						text.append(String.format("%02x", value));
					}
					bytes = text.toString();
				}
				catch (Exception exception) {
					bytes = "no bytes";
				}
			}

			println(String.format("%-10s block=%-10s bytes=%s at=%s containing=%s function=%s",
				"0x" + Long.toHexString(offset),
				block == null ? "-" : block.getName(),
				bytes,
				instruction == null ? "-" : "yes",
				containing == null ? "-" : "yes",
				function == null ? "-" : function.getName()));
		}

		for (long[] window : WINDOWS) {
			int instructions = 0;
			int functions = 0;
			Address start = toAddr(window[0]);
			Address end = toAddr(window[1]);
			for (Instruction ignored : currentProgram.getListing().getInstructions(start, true)) {
				if (ignored.getAddress().compareTo(end) >= 0) {
					break;
				}
				instructions++;
			}
			for (Function ignored : currentProgram.getFunctionManager().getFunctions(start, true)) {
				if (ignored.getEntryPoint().compareTo(end) >= 0) {
					break;
				}
				functions++;
			}
			println(String.format("window %s-%s: %d instructions, %d functions",
				start, end, instructions, functions));
		}
	}
}
