// Probe helper for the MGO2 p2p join investigation.
// Usage (headless -postScript):
//   Mgo2Probe.java decomp 0x26ab58 0x26b0a4 ...
//   Mgo2Probe.java refs 0x26ab58 ...
//   Mgo2Probe.java offset 0xb 0xc
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.listing.InstructionIterator;
import ghidra.program.model.scalar.Scalar;
import ghidra.program.model.symbol.Reference;
import java.util.*;

public class Mgo2Probe extends GhidraScript {
    private DecompInterface decomp;

    private DecompInterface dec() {
        if (decomp == null) {
            decomp = new DecompInterface();
            decomp.openProgram(currentProgram);
        }
        return decomp;
    }

    private void dumpFunction(Address addr) {
        ghidra.program.model.mem.MemoryBlock blk = currentProgram.getMemory().getBlock(addr);
        println("  [block of " + addr + " = " + (blk == null ? "NONE" : blk.getName() + " " + blk.getStart() + ".." + blk.getEnd() + " init=" + blk.isInitialized()) + "]");
        Function f = getFunctionContaining(addr);
        if (f == null) {
            println("=== no function contains " + addr + " ===");
            return;
        }
        println("===== function " + f.getName() + " @ " + f.getEntryPoint() + " (query " + addr + ") =====");
        DecompileResults r = dec().decompileFunction(f, 90, monitor);
        if (!r.decompileCompleted() || r.getDecompiledFunction() == null) {
            println("  decompile failed: " + r.getErrorMessage());
            return;
        }
        println(r.getDecompiledFunction().getC());
    }

    private void refs(Address addr) {
        println("===== references to " + addr + " =====");
        Reference[] rs = getReferencesTo(addr);
        if (rs.length == 0) {
            println("  (none)");
            return;
        }
        for (Reference ref : rs) {
            Address from = ref.getFromAddress();
            Function f = getFunctionContaining(from);
            println("  from " + from + "  type=" + ref.getReferenceType()
                    + "  in " + (f == null ? "?" : f.getName() + "@" + f.getEntryPoint()));
        }
    }

    private void offsets(List<Long> offs) {
        Set<Long> wanted = new HashSet<>(offs);
        println("===== byte stores/loads at displacements " + wanted + " =====");
        InstructionIterator it = currentProgram.getListing().getInstructions(true);
        while (it.hasNext()) {
            Instruction ins = it.next();
            String mn = ins.getMnemonicString().toLowerCase();
            boolean store = mn.startsWith("stb");
            boolean load = mn.startsWith("lbz");
            if (!store && !load) {
                continue;
            }
            int n = ins.getNumOperands();
            for (int i = 0; i < n; i++) {
                Object[] objs = ins.getOpObjects(i);
                for (Object o : objs) {
                    if (o instanceof Scalar) {
                        long v = ((Scalar) o).getUnsignedValue();
                        if (wanted.contains(v)) {
                            Function f = getFunctionContaining(ins.getAddress());
                            println((store ? "  ST " : "  LD ") + ins.getAddress() + "  " + ins
                                    + "   [offset 0x" + Long.toHexString(v) + "]"
                                    + "  in " + (f == null ? "?" : f.getName() + "@" + f.getEntryPoint()));
                        }
                    }
                }
            }
        }
    }

    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length == 0) {
            println("no args; pass decomp|refs|offset");
            return;
        }
        String mode = args[0];
        if ("decomp".equals(mode)) {
            for (int i = 1; i < args.length; i++) {
                dumpFunction(toAddr(Long.decode(args[i])));
            }
        } else if ("refs".equals(mode)) {
            for (int i = 1; i < args.length; i++) {
                refs(toAddr(Long.decode(args[i])));
            }
        } else if ("stats".equals(mode)) {
            ghidra.program.model.listing.FunctionManager fm = currentProgram.getFunctionManager();
            println("  function count = " + fm.getFunctionCount());
            int shown = 0;
            for (Function f : fm.getFunctions(true)) {
                println("  " + f.getEntryPoint() + "  " + f.getName());
                if (++shown >= 25) break;
            }
            ghidra.program.model.symbol.SymbolTable st = currentProgram.getSymbolTable();
            int named = 0;
            for (ghidra.program.model.symbol.Symbol s : st.getAllSymbols(true)) {
                if (s.getName().startsWith("FUN_") && ++named >= 1) { println("  first FUN_ symbol: " + s.getAddress() + " " + s.getName()); break; }
            }
        } else if ("blocks".equals(mode)) {
            for (ghidra.program.model.mem.MemoryBlock b : currentProgram.getMemory().getBlocks()) {
                println("  " + b.getName() + "  " + b.getStart() + " .. " + b.getEnd() + "  init=" + b.isInitialized() + " exec=" + b.isExecute());
            }
        } else if ("funcs".equals(mode)) {
            long lo = Long.decode(args[1]);
            long hi = Long.decode(args[2]);
            ghidra.program.model.address.Address a = toAddr(lo);
            while (a != null && a.getOffset() < hi) {
                Function f = getFunctionContaining(a);
                if (f != null) {
                    println("  " + f.getEntryPoint() + "  " + f.getName());
                    a = f.getBody().getMaxAddress().add(1);
                } else {
                    a = a.add(4);
                }
            }
        } else if ("offset".equals(mode)) {
            List<Long> offs = new ArrayList<>();
            for (int i = 1; i < args.length; i++) {
                offs.add(Long.decode(args[i]));
            }
            offsets(offs);
        } else {
            println("unknown mode " + mode);
        }
    }
}
