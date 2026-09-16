// original call TARGET 0x00a0ccb8

bool _opd_FUN_00a0ccb8(int param_1,ulonglong param_2)

{
  int iVar1;
  bool bVar2;
  
  bVar2 = false;
  if ((((uint)param_2 & *(uint *)(param_1 * 4 + *(int *)(PTR_PTR_0121bb48 + -0x8000) + 200)) != 0)
     && (bVar2 = true, (param_2 >> 2 & 1) != 0)) {
    iVar1 = _opd_FUN_0097d170();
    bVar2 = iVar1 == 0;
  }
  return bVar2;
}

