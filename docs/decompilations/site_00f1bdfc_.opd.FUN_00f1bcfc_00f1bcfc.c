// hook SITE 0x00f1bdfc

int _opd_FUN_00f1bcfc(int param_1)

{
  int *piVar1;
  int *piVar2;
  int iVar3;
  int *piVar4;
  uint local_50 [6];
  
  iVar3 = -0x18;
  if (param_1 != 0) {
    iVar3 = -0x46;
    piVar1 = (int *)_opd_FUN_00f04920(param_1);
    piVar2 = (int *)_opd_FUN_00f06488(param_1);
    if (*piVar1 == 0x4942) {
      iVar3 = -0x3ef;
      piVar4 = (int *)(param_1 + 0x22a08);
      if (*piVar4 != 0) {
        _opd_FUN_00f2ddec(piVar1);
        iVar3 = _opd_FUN_00f17340(param_1,piVar4,piVar1);
        if (iVar3 == 0) {
          iVar3 = _opd_FUN_00f2e280(piVar1,local_50);
          if (iVar3 == 0) {
            _opd_FUN_00f2de00(piVar1);
            if (local_50[0] < 8) {
              _opd_FUN_00effa0c(param_1,7,local_50[0]);
              iVar3 = 0;
              if (piVar4[local_50[0] * 8 + 100] == *piVar2) {
                _opd_FUN_00f18b60(param_1);
                _opd_FUN_00f1eee0(param_1);
              }
              else {
                _opd_FUN_00fa4d48(piVar4 + local_50[0] * 8 + 100,0,0x20);
              }
            }
            else {
              iVar3 = -0x40c;
            }
          }
          else {
            iVar3 = -0x47;
          }
        }
      }
    }
  }
  return iVar3;
}

