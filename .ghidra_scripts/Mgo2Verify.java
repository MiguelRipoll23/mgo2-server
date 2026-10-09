// Reports what the MGO2 project holds, so a run that opens it can be told from
// one that has nothing to read. Disassembles the addresses the protocol work
// depends on and decompiles the function around one of them.
//
// Usage:
//   analyzeHeadless.bat <projectDir> MGO2 -process MGO2.ELF -noanalysis \
//       -scriptPath <repo>\.ghidra_scripts -postScript Mgo2Verify.java
import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionManager;
import ghidra.program.model.listing.Instruction;

public class Mgo2Verify extends GhidraScript {

	@Override
	public void run() throws Exception {
		println("program          = " + currentProgram.getName());
		println("language         = " + currentProgram.getLanguageID()
			+ " (" + currentProgram.getCompilerSpec().getCompilerSpecID() + ")");
		println("imageBase        = " + currentProgram.getImageBase());
		println("memoryBlocks     = " + currentProgram.getMemory().getBlocks().length);

		FunctionManager functions = currentProgram.getFunctionManager();
		println("functions        = " + functions.getFunctionCount());
		println("instructions     = " + currentProgram.getListing().getNumInstructions());

		// The addresses this repository's protocol documents cite, and what the
		// project has to say about each.
		long[] cited = { 0xaa1140L, 0x268f18L, 0x26ab58L, 0x268c58L, 0x2676e0L, 0x266f14L };
		for (long offset : cited) {
			Address address = toAddr(offset);
			Instruction instruction = currentProgram.getListing().getInstructionAt(address);
			Function function = functions.getFunctionContaining(address);
			println(String.format("%-10s %-28s %s",
				"0x" + Long.toHexString(offset),
				instruction == null ? "not disassembled" : instruction.toString(),
				function == null ? "(no function)" : function.getName() + " @ " + function.getEntryPoint()));
		}

		Function window = functions.getFunctionContaining(toAddr(0x26ab58L));
		if (window == null) {
			println("decompile        = no function at 0x26ab58");
			return;
		}

		DecompInterface decompiler = new DecompInterface();
		decompiler.openProgram(currentProgram);
		DecompileResults results = decompiler.decompileFunction(window, 120, monitor);
		if (results == null || !results.decompileCompleted()) {
			println("decompile        = failed: " + (results == null ? "null" : results.getErrorMessage()));
			return;
		}

		String source = results.getDecompiledFunction().getC();
		println("decompile        = ok, " + source.length() + " chars from " + window.getName());
		println(source.length() > 1200 ? source.substring(0, 1200) : source);
	}
}
