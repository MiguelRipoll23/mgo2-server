/* containing function of static patch target(s); entry .opd.FUN_00264270 @ 00264270 */

int _opd_FUN_00264270(int param_1,undefined4 param_2)

{
  int iVar1;
  
  if (param_1 == 7) {
    iVar1 = _opd_FUN_0026d350(0x6e0,2);
    if (iVar1 != 0) {
      _opd_FUN_002854a0(iVar1,param_2);
      return iVar1;
    }
  }
  else if (param_1 == 8) {
    iVar1 = _opd_FUN_0026d350(0x8f0,2);
    if (iVar1 != 0) {
      _opd_FUN_00286358(iVar1);
      return iVar1;
    }
  }
  else if ((param_1 == 6) && (iVar1 = _opd_FUN_0026d350(0x6e8,2), iVar1 != 0)) {
    _opd_FUN_00284cb0(iVar1);
    return iVar1;
  }
  return 0;
}

