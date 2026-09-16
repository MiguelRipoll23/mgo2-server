// original call TARGET 0x005e68c8

int _opd_FUN_005e68c8(uint param_1,int param_2,int param_3)

{
  int iVar1;
  
  param_1 = param_1 | param_2 << 0x18;
  if (((param_3 == 0) || (iVar1 = _opd_FUN_000c4f18(param_1,param_3), iVar1 == 0)) &&
     (iVar1 = _opd_FUN_000c51b0(param_1), iVar1 == 0)) {
    iVar1 = _opd_FUN_000c50d8(param_1);
    return iVar1;
  }
  return iVar1;
}

