// original call TARGET 0x000c5ab0

int _opd_FUN_000c5ab0(uint param_1)

{
  int *piVar1;
  int iVar2;
  
  piVar1 = *(int **)((param_1 >> 1 & 0x3c) + *(int *)(PTR_PTR_0121ac80 + -0x8000) + 8);
  if (piVar1 != (int *)0x0) {
    do {
      if ((int)((param_1 & 0x7fffffff) - (piVar1[2] & 0x7fffffffU)) < 0) {
        piVar1 = (int *)*piVar1;
      }
      else {
        if (((param_1 & 0x7fffffff) == (piVar1[2] & 0x7fffffffU)) &&
           (iVar2 = _opd_FUN_000c30e8(piVar1,0,0,0,0), iVar2 != 0)) {
          return piVar1[5];
        }
        piVar1 = (int *)piVar1[1];
      }
    } while (piVar1 != (int *)0x0);
  }
  return 0;
}

