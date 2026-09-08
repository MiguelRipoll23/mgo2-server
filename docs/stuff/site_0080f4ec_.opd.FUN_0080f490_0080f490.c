// hook SITE 0x0080f4ec

undefined8 _opd_FUN_0080f490(int param_1,undefined4 param_2)

{
  float fVar1;
  float fVar2;
  float fVar3;
  float *pfVar4;
  float *pfVar5;
  ushort uVar6;
  undefined *puVar7;
  uint uVar8;
  int iVar9;
  int iVar10;
  uint uVar11;
  byte local_80;
  char local_7f;
  short local_7e;
  short local_7c;
  short local_7a;
  short local_78;
  short local_76;
  short local_74;
  ushort local_72 [7];
  float fStack_64;
  
  puVar7 = PTR_PTR_0121b844;
  _opd_FUN_00289558(param_1 + 0x9d0);
  _opd_FUN_0026cc88(param_2,local_72,2);
  _opd_FUN_0026cc88(param_2,&local_78,2);
  _opd_FUN_0026cc88(param_2,&local_76,2);
  _opd_FUN_0026cc88(param_2,&local_74,2);
  uVar11 = local_72[0] >> 5 & 0x3ff;
  fVar1 = *(float *)(puVar7 + -0x7fcc);
  *(float *)(param_1 + 0x810) = (float)(longlong)local_78 * fVar1;
  *(float *)(param_1 + 0x814) = (float)(longlong)local_76 * fVar1;
  *(float *)(param_1 + 0x818) = (float)(longlong)local_74 * fVar1;
  _opd_FUN_0026cc88(param_2,param_1 + 0x820,2);
  _opd_FUN_0026cc88(param_2,&local_7f,1);
  _opd_FUN_0026cc88(param_2,&local_80,1);
  iVar9 = _opd_FUN_002f8250(param_1,0x1e05d5);
  if ((iVar9 == 0) || (*(int *)(iVar9 + 0x30) != 0)) {
    iVar9 = _opd_FUN_002f8250(param_1,0x7e8110);
    fVar1 = *(float *)(puVar7 + -0x8000);
    *(float *)(iVar9 + 0x250) = (float)(longlong)local_7f * fVar1;
    *(float *)(iVar9 + 0x254) = (float)local_80 * fVar1;
  }
  else {
    fVar1 = *(float *)(puVar7 + -0x8000);
    *(float *)(iVar9 + 0x114) = (float)(longlong)local_7f * fVar1;
    *(float *)(iVar9 + 0x118) = (float)local_80 * fVar1;
  }
  iVar9 = _opd_FUN_002f8250(param_1,0x80dd2a);
  if (iVar9 != 0) {
    *(undefined4 *)(iVar9 + 0xf0) = 0xffffffff;
  }
  if (0x13b < uVar11) {
    if (uVar11 == 0x1e5) {
LAB_0080f798:
      iVar9 = _opd_FUN_0026cc88(param_2,&local_7e,1);
      if (iVar9 != 0) {
        iVar9 = _opd_FUN_002f8250(param_1,0xdc8d0a);
        fVar1 = *(float *)(puVar7 + -0x7ff0);
        fVar2 = fVar1;
        if ((float)(longlong)(char)local_7e._0_1_ * *(float *)(puVar7 + -0x7ff8) - fVar1 < 0.0) {
          fVar2 = (float)(longlong)(char)local_7e._0_1_ * *(float *)(puVar7 + -0x7ff8);
        }
        if (fVar2 + fVar1 < 0.0) {
          fVar2 = *(float *)(puVar7 + -0x7fe8);
        }
        *(float *)(iVar9 + 0xd8) = fVar2 + *(float *)(puVar7 + -0x7fc4);
      }
    }
    else {
      if (uVar11 < 0x1e6) {
        if (0x198 < uVar11) {
          if (uVar11 != 0x1df) {
            if (uVar11 < 0x1e0) {
              if (uVar11 - 0x1c2 < 2) {
                _opd_FUN_0026cc88(param_2,&local_7e,1);
                iVar9 = _opd_FUN_002f8250(param_1,0x7e0b7a);
                *(int *)(iVar9 + 0x54) = (int)(char)local_7e._0_1_;
              }
              goto LAB_0080f6d0;
            }
            if ((uVar11 != 0x1e1) && (uVar11 != 0x1e3)) goto LAB_0080f6d0;
          }
          goto LAB_0080f798;
        }
        if (0x195 < uVar11) {
          iVar9 = _opd_FUN_0026cc88(param_2,&local_7e,1);
          if (iVar9 != 0) {
            iVar9 = _opd_FUN_002f8250(param_1,0x73deac);
            *(int *)(iVar9 + 0x4c) = (int)(char)local_7e._0_1_;
          }
          goto LAB_0080f6d0;
        }
        if (uVar11 < 0x15b) {
          if (uVar11 < 0x154) {
            if (uVar11 < 0x143) {
              _opd_FUN_0026cc88(param_2,&local_7e,1);
              iVar9 = _opd_FUN_00308310(local_7e._0_1_);
              iVar10 = _opd_FUN_002f8250(param_1,0x80dd2a);
              *(undefined4 *)(iVar10 + 0x88) = *(undefined4 *)(iVar9 + 0x10);
            }
          }
          else {
            iVar9 = _opd_FUN_0026cc88(param_2,&local_7e,1);
            if (iVar9 != 0) {
              iVar9 = _opd_FUN_002f8250(param_1,0x6cf4bc);
              *(int *)(iVar9 + 0x7c) = (int)(char)local_7e._0_1_;
            }
          }
          goto LAB_0080f6d0;
        }
        if (5 < uVar11 - 0x18d) goto LAB_0080f6d0;
      }
      else {
        if (uVar11 < 0x210) {
          if (0x20d < uVar11) {
            iVar9 = _opd_FUN_0026cc88(param_2,&local_7c,1);
            if (iVar9 != 0) {
              iVar9 = _opd_FUN_002f8250(param_1,0x236d7e);
              *(int *)(iVar9 + 0x48) = (int)local_7c._0_1_;
            }
            goto LAB_0080f6d0;
          }
          if (uVar11 < 0x1ed) {
            if (uVar11 < 0x1eb) {
              if ((uVar11 != 0x1e7) && (uVar11 != 0x1e9)) goto LAB_0080f6d0;
              goto LAB_0080f798;
            }
          }
          else if (3 < uVar11 - 0x1ee) goto LAB_0080f6d0;
          iVar9 = _opd_FUN_0026cc88(param_2,&local_7c,1);
          if (iVar9 != 0) {
            iVar9 = _opd_FUN_002f8250(param_1,0x7b5f88);
            *(int *)(iVar9 + 0x4c) = (int)local_7c._0_1_;
          }
          goto LAB_0080f6d0;
        }
        if (uVar11 < 0x2c2) {
          if (uVar11 < 0x2c0) {
            if (((uVar11 - 0x213 < 3) &&
                (iVar9 = _opd_FUN_002f8250(param_1,&DAT_00de95a4), iVar9 != 0)) &&
               (iVar10 = _opd_FUN_0026cc88(param_2,&local_7a,2), iVar10 != 0)) {
              _opd_FUN_0026cc88(param_2,&local_7e,2);
              _opd_FUN_0026cc88(param_2,&local_7c,2);
              pfVar4 = *(float **)(puVar7 + -0x7fe0);
              fVar1 = pfVar4[1];
              fVar2 = pfVar4[2];
              fVar3 = *(float *)(puVar7 + -0x7fcc);
              pfVar5 = (float *)(iVar9 + 0xc0U & 0xfffffff0);
              *pfVar5 = (float)(longlong)local_7a * fVar3 + *pfVar4;
              pfVar5[1] = (float)(longlong)local_7e * fVar3 + fVar1;
              pfVar5[2] = (float)(longlong)local_7c * fVar3 + fVar2;
              pfVar5[3] = fStack_64;
              *(ulonglong *)(iVar9 + 0xd0) = *(ulonglong *)(iVar9 + 0xd0) | 0x8000000000000000;
            }
            goto LAB_0080f6d0;
          }
          goto LAB_0080f69c;
        }
        if (uVar11 == 0x2cc) {
          _opd_FUN_0026cc88(param_2,&local_7c,1);
          iVar9 = _opd_FUN_002f8250(param_1,0x7e8110);
          *(int *)(iVar9 + 0x24c) = (int)local_7c._0_1_;
          goto LAB_0080f6d0;
        }
        if (uVar11 != 0x2cd) goto LAB_0080f6d0;
      }
      iVar9 = _opd_FUN_0026cc88(param_2,&local_7c,2);
      if (iVar9 != 0) {
        iVar9 = _opd_FUN_002f8250(param_1,0x73deac);
        *(short *)(iVar9 + 0x58) = local_7c;
      }
    }
    goto LAB_0080f6d0;
  }
  if (uVar11 < 0x113) {
    if (uVar11 < 0x67) {
      if (uVar11 < 0x65) {
        if (uVar11 < 0x4e) {
          if (uVar11 < 0x4b) {
            if (0x3e < uVar11) {
              if (uVar11 < 0x42) {
                _opd_FUN_0026cc88(param_2,&local_7e,1);
                iVar9 = _opd_FUN_002f8250(param_1,0x7e8110);
                *(int *)(iVar9 + 0x240) = (int)(char)local_7e._0_1_;
              }
              else if ((uVar11 - 0x46 < 3) &&
                      (iVar9 = _opd_FUN_0026cc88(param_2,&local_7e,1), iVar9 != 0)) {
                iVar9 = _opd_FUN_002f8250(param_1,0xcf5135);
                *(ulonglong *)(iVar9 + 0x5f0) =
                     -(ulonglong)local_7e._0_1_ & 0x8000000000000000 |
                     *(ulonglong *)(iVar9 + 0x5f0) & 0x7fffffffffffffff;
              }
            }
          }
          else {
            iVar9 = _opd_FUN_0026cc88(param_2,&local_7e,1);
            if (iVar9 != 0) {
              iVar9 = _opd_FUN_002f8250(param_1,0xcf5135);
              *(int *)(iVar9 + 0x5ec) = (int)(char)local_7e._0_1_;
            }
          }
          goto LAB_0080f6d0;
        }
        if (0x60 < uVar11) {
          uVar8 = uVar11 - 0x62;
          goto joined_r0x0080fe3c;
        }
        if (uVar11 < 0x5f) {
          if ((uVar11 - 0x50 < 2) && (iVar9 = _opd_FUN_0026cc88(param_2,&local_7c,1), iVar9 != 0)) {
            iVar9 = _opd_FUN_002f8250(param_1,0x57afc8);
            *(int *)(iVar9 + 0x48) = (int)local_7c._0_1_;
          }
          goto LAB_0080f6d0;
        }
      }
    }
    else {
      if (0x72 < uVar11) {
        if (uVar11 < 0xaa) {
          if (0x99 < uVar11) {
            iVar9 = _opd_FUN_0026cc88(param_2,&local_7c,1);
            if (iVar9 != 0) {
              iVar9 = _opd_FUN_002f8250(param_1,0x975d91);
              _opd_FUN_0026cc88(param_2,&local_7e,1);
              *(int *)(iVar9 + 0x128) = (int)local_7c._0_1_;
              *(ushort *)(iVar9 + 0x82) = (ushort)local_7e._0_1_;
            }
            goto LAB_0080f6d0;
          }
          uVar8 = uVar11 - 0x74;
          goto joined_r0x0080fe3c;
        }
        if (0x15 < uVar11 - 0xfc) goto LAB_0080f6d0;
        goto LAB_0080f69c;
      }
      if (uVar11 < 0x71) {
        if (uVar11 < 0x6d) {
          if (0x6a < uVar11) goto LAB_0080f890;
          uVar8 = uVar11 - 0x68;
        }
        else {
          uVar8 = uVar11 - 0x6e;
        }
joined_r0x0080fe3c:
        if (1 < uVar8) goto LAB_0080f6d0;
      }
    }
LAB_0080f890:
    iVar9 = _opd_FUN_0026cc88(param_2,&local_7e,1);
    if (iVar9 != 0) {
      iVar9 = _opd_FUN_002f8250(param_1,0xd9728f);
      *(ulonglong *)(iVar9 + 0x260) =
           (-(ulonglong)local_7e._0_1_ >> 0x3f) << 0x3e |
           *(ulonglong *)(iVar9 + 0x260) & 0xbfffffffffffffff;
    }
  }
  else {
LAB_0080f69c:
    _opd_FUN_0026cc88(param_2,&local_7e,1);
    iVar9 = _opd_FUN_002f8250(param_1,0x80dd2a);
    *(int *)(iVar9 + 0xf0) = (int)(char)local_7e._0_1_;
  }
LAB_0080f6d0:
  uVar6 = local_72[0] >> 3 & 3;
  iVar9 = *(int *)(param_1 + 0x34);
  *(uint *)(param_1 + 0x804) = local_72[0] & 7;
  if (uVar6 == 1) {
    *(undefined4 *)(iVar9 + 0x184) = *(undefined4 *)(puVar7 + -0x7fdc);
  }
  else if (uVar6 == 2) {
    *(undefined4 *)(iVar9 + 0x184) = *(undefined4 *)(puVar7 + -0x7fd4);
  }
  else {
    *(undefined4 *)(iVar9 + 0x184) = *(undefined4 *)(iVar9 + 0x180);
  }
  *(uint *)(param_1 + 0x824) = uVar11;
  *(byte *)(param_1 + 0x830) = (byte)(local_72[0] >> 0xf);
  if (uVar11 != 0x30) {
    if (uVar11 == 0x31) {
      *(undefined4 *)(param_1 + 0x824) = 0x1b;
    }
    return 0;
  }
  *(undefined4 *)(param_1 + 0x824) = 3;
  return 0;
}

