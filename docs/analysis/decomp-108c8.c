// function FUN_000108c8 @ 000108c8 (target 10940)

undefined8 FUN_000108c8(undefined8 param_1)

{
  longlong lVar1;
  undefined1 auStack_110 [256];
  
  lVar1 = FUN_001a2a90(0x1038);
  if (lVar1 != 0) {
    FUN_001a7d08(auStack_110,0x100,uRam001eb980,param_1);
                    /* WARNING: Subroutine does not return */
    FUN_00013c58(uRam001eb988,auStack_110);
  }
  return 0;
}

