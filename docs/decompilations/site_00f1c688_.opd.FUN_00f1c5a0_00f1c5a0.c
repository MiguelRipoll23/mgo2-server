// hook SITE 0x00f1c688

int _opd_FUN_00f1c5a0(int param_1)

{
  int *piVar1;
  int iVar2;
  int *piVar3;
  uint local_40 [4];
  
  iVar2 = -0x18;
  if (param_1 != 0) {
    piVar1 = (int *)_opd_FUN_00f04920(param_1);
    piVar3 = (int *)(param_1 + 0x22a08);
    iVar2 = -0x46;
    if ((*piVar1 == 0x4919) && (iVar2 = -0x3ef, *piVar3 != 0)) {
      _opd_FUN_00f2ddec(piVar1);
      iVar2 = _opd_FUN_00f17340(param_1,piVar3,piVar1);
      if (iVar2 == 0) {
        iVar2 = _opd_FUN_00f2e280(piVar1,local_40);
        if (iVar2 == 0) {
          _opd_FUN_00f2de00(piVar1);
          if (local_40[0] < 8) {
            _opd_FUN_00effa0c(param_1,2,local_40[0]);
            _opd_FUN_00fa4d48(piVar3 + local_40[0] * 8 + 100,0,0x20);
            iVar2 = 0;
          }
          else {
            iVar2 = -0x40c;
          }
        }
        else {
          iVar2 = -0x47;
        }
      }
    }
  }
  return iVar2;
}

